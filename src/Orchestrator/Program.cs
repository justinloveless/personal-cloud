using System.Diagnostics;
using Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

if (args.Length < 1 || args[0] is not ("tenant"))
{
    Console.WriteLine("Usage: orchestrator tenant create --subdomain <name>");
    return;
}

var command = args.ElementAtOrDefault(1);
if (command != "create")
{
    Console.WriteLine("Supported: tenant create --subdomain <name>");
    return;
}

var subFlagIdx = Array.IndexOf(args, "--subdomain");
if (subFlagIdx < 0 || subFlagIdx == args.Length - 1)
{
    Console.WriteLine("Missing --subdomain");
    return;
}
var sub = args[subFlagIdx + 1];
var dbName = $"tenant_{sub}".ToLowerInvariant();

var adminCs = Environment.GetEnvironmentVariable("ConnectionStrings__Admin")
            ?? "Host=localhost;Port=5432;Username=app;Password=app;Database=admin";

await using var admin = new NpgsqlConnection(adminCs);
await admin.OpenAsync();

// 1) Create tenant database
await using (var cmd = new NpgsqlCommand($"create database \"{dbName}\" owner \"app\";", admin))
{
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Created DB {dbName}");
}

// 2) Apply EF migrations programmatically against the tenant DB
var csb = new NpgsqlConnectionStringBuilder(adminCs) { Database = dbName }.ToString();
var opts = new DbContextOptionsBuilder<Shared.Persistence.AppDbContext>()
    .UseNpgsql(csb)
    .Options;

using (var ctx = new Shared.Persistence.AppDbContext(opts))
{
    var hasMigrations = ctx.Database.GetMigrations().Any();
    if (hasMigrations)
    {
        await ctx.Database.MigrateAsync();
        Console.WriteLine("Applied migrations.");
    }
    else
    {
        await ctx.Database.EnsureCreatedAsync();
        Console.WriteLine("Ensured schema created (no migrations found).");
    }
}

// 3) Register tenant in admin.tenants
await using (var cmd = new NpgsqlCommand("""
    insert into tenants (subdomain, db_name)
    values (@sub, @db)
    on conflict (subdomain) do nothing;
""", admin))
{
    cmd.Parameters.AddWithValue("sub", sub);
    cmd.Parameters.AddWithValue("db", dbName);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Registered tenant {sub}");
}

Console.WriteLine("Done.");
