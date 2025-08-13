using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Shared.Tenancy;

public record TenantContext(string Id, string Subdomain, string DbName, string Plan, string Region);

public interface ITenantDirectory
{
    Task<TenantContext?> GetAsync(string subdomain, CancellationToken ct = default);
}

public class TenantDirectory(IConfiguration cfg, ILogger<TenantDirectory> log) : ITenantDirectory
{
    private readonly string _adminCs = cfg.GetConnectionString("Admin")!;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1024 });

    public async Task<TenantContext?> GetAsync(string subdomain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return null;
        if (_cache.TryGetValue(subdomain, out TenantContext value)) return value;

        await using var conn = new NpgsqlConnection(_adminCs);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            select id::text, subdomain, db_name, plan, region
            from tenants where subdomain = @sub
            """,
            conn);
        cmd.Parameters.AddWithValue("sub", subdomain);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var ctx = new TenantContext(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4)
        );

        _cache.Set(subdomain, ctx, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), Size = 1 });
        return ctx;
    }
}


