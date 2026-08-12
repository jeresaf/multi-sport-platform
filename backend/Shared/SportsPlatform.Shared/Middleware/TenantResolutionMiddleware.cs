using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SportsPlatform.Shared.Configuration;

namespace SportsPlatform.Shared.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantSettings _settings;

    public TenantResolutionMiddleware(RequestDelegate next, IOptions<TenantSettings> settings)
    {
        _next = next;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Guid? tenantId = null;

        // Try to resolve tenant from header
        if (context.Request.Headers.TryGetValue(_settings.TenantHeaderName, out var headerValue))
        {
            if (Guid.TryParse(headerValue, out var parsedId))
            {
                tenantId = parsedId;
            }
        }

        // Try to resolve tenant from claim (authenticated user)
        if (tenantId == null && context.User?.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id");
            if (claim != null && Guid.TryParse(claim.Value, out var parsedClaimId))
            {
                tenantId = parsedClaimId;
            }
        }

        // Store tenant ID in HttpContext items
        if (tenantId.HasValue)
        {
            context.Items["TenantId"] = tenantId.Value;
        }

        await _next(context);
    }
}

public static class TenantContext
{
    private static readonly AsyncLocal<Guid?> _currentTenantId = new();

    public static Guid? CurrentTenantId
    {
        get => _currentTenantId.Value;
        set => _currentTenantId.Value = value;
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }
}