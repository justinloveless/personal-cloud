using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Shared.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var adminCs = Environment.GetEnvironmentVariable("ConnectionStrings__Admin")
            ?? "Host=localhost;Port=5432;Username=app;Password=app;Database=admin";

        // Use a stable database for model snapshot generation
        var cs = new NpgsqlConnectionStringBuilder(adminCs) { Database = "postgres" }.ToString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new AppDbContext(options);
    }
}


