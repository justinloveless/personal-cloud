using Shared.Tenancy;
using Shared.Persistence;
using Shared.Domain;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantDirectory, TenantDirectory>();

// Per-request DbContext bound to the tenant's database via PgBouncer
builder.Services.AddScoped(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext
               ?? throw new InvalidOperationException("No HttpContext");
    var tenant = (TenantContext)http.Items["Tenant"]!;
    var baseCs = builder.Configuration.GetConnectionString("PgBouncer")!;
    var csb = new NpgsqlConnectionStringBuilder(baseCs) { Database = tenant.DbName };
    var opts = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(csb.ToString())
        .Options;
    return new AppDbContext(opts);
});

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();

string adminApiKey = builder.Configuration["AdminApiKey"]
    ?? Environment.GetEnvironmentVariable("ADMIN_API_KEY")
    ?? throw new InvalidOperationException("Missing Admin API key (AdminApiKey)");

app.MapPost("/api/admin/tenants", async (HttpContext ctx) =>
{
    var provided = ctx.Request.Headers["x-api-key"].ToString();
    if (string.IsNullOrWhiteSpace(provided) || provided != adminApiKey)
    {
        return Results.Unauthorized();
    }

    var sub = ctx.Request.Query["subdomain"].ToString();
    if (string.IsNullOrWhiteSpace(sub))
    {
        return Results.BadRequest(new { error = "Missing subdomain" });
    }

    var adminCs = builder.Configuration.GetConnectionString("Admin")!;

    await using var admin = new NpgsqlConnection(adminCs);
    await admin.OpenAsync();

    var dbName = $"tenant_{sub}".ToLowerInvariant();

    // 1) Create tenant database (idempotent-ish: ignore if exists)
    try
    {
        await using var createCmd = new NpgsqlCommand($"create database \"{dbName}\" owner \"app\";", admin);
        await createCmd.ExecuteNonQueryAsync();
    }
    catch (PostgresException ex) when (ex.SqlState == "42P04") { /* duplicate_database */ }

    // 2) Apply EF migrations programmatically against the tenant DB
    var csb = new NpgsqlConnectionStringBuilder(adminCs) { Database = dbName }.ToString();
    var opts = new DbContextOptionsBuilder<Shared.Persistence.AppDbContext>()
        .UseNpgsql(csb)
        .Options;
    using (var ctxDb = new AppDbContext(opts))
    {
        await ctxDb.Database.MigrateAsync();
    }

    // 3) Register tenant in admin.tenants
    await using (var upsert = new NpgsqlCommand(@"insert into tenants (subdomain, db_name)
values (@sub, @db) on conflict (subdomain) do nothing;", admin))
    {
        upsert.Parameters.AddWithValue("sub", sub);
        upsert.Parameters.AddWithValue("db", dbName);
        await upsert.ExecuteNonQueryAsync();
    }

    return Results.Ok(new { subdomain = sub, db = dbName });
});

app.MapGet("/api/hello", (HttpContext ctx) =>
{
    var t = (TenantContext)ctx.Items["Tenant"]!;
    return Results.Ok(new { tenant = t.Subdomain, db = t.DbName, plan = t.Plan, region = t.Region });
});

app.MapGet("/api/notes", async (AppDbContext db) =>
{
    var items = await db.Notes.OrderByDescending(n => n.Id).ToListAsync();
    return Results.Ok(items);
});

app.MapPost("/api/notes", async (AppDbContext db, Note input) =>
{
    db.Notes.Add(input);
    await db.SaveChangesAsync();
    return Results.Created($"/api/notes/{input.Id}", input);
});

app.Run();
