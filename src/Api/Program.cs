using Api.Infrastructure;
using Api.Infrastructure.Models;
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
    var opts = new DbContextOptionsBuilder<Api.Infrastructure.AppDbContext>()
        .UseNpgsql(csb.ToString())
        .Options;
    return new AppDbContext(opts);
});

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();

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
