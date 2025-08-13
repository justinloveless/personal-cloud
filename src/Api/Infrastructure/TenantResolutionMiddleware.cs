using Microsoft.AspNetCore.Http;
using Shared.Tenancy;

namespace Api.Infrastructure;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantDirectory directory)
    {
        // Bypass tenant resolution for admin endpoints
        if (context.Request.Path.StartsWithSegments("/api/admin"))
        {
            await next(context);
            return;
        }

        var host = context.Request.Host.Host; // e.g., "acme.yourapp.com"
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // handle localhost and direct IPs
        var subdomain = parts.Length >= 3 ? parts[0] : (context.Request.Headers.TryGetValue("X-Tenant", out var t) ? t.ToString() : "");

        var tenant = await directory.GetAsync(subdomain);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Unknown tenant");
            return;
        }

        context.Items["Tenant"] = tenant;
        await next(context);
    }
}
