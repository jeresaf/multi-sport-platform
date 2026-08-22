using System.Security.Claims;
using Serilog.Context;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantRepository tenantRepository)
    {
        // A platform operator may explicitly enter a tenant workspace. Validate
        // the requested tenant and replace only the request-scoped tenant claim;
        // the signed token and platform role claims remain unchanged.
        var requestedTenant = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.IsPlatformOperator()
            && !string.IsNullOrWhiteSpace(requestedTenant))
        {
            if (!Guid.TryParse(requestedTenant, out var selectedTenantId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "The selected tenant ID is invalid." });
                return;
            }
            var selectedTenant = await tenantRepository.GetByIdAsync(
                selectedTenantId,
                context.RequestAborted);
            if (selectedTenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "The selected tenant does not exist."
                });
                return;
            }

            if (context.User.Identity is ClaimsIdentity identity)
            {
                foreach (var claim in identity.FindAll("tenant_id").ToArray())
                    identity.RemoveClaim(claim);
                identity.AddClaim(new Claim("tenant_id", selectedTenantId.ToString()));
            }
            context.Items["PlatformTenantScope"] = selectedTenantId;
        }

        // Try to resolve tenant from JWT claim first
        Guid? tenantId = null;
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
        }

        // Fall back to X-Tenant-Id header for public/unauthenticated routes
        if (tenantId == null)
        {
            var headerTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenantId) && Guid.TryParse(headerTenantId, out var headerParsedTenantId))
            {
                tenantId = headerParsedTenantId;
            }
        }

        // Fall back to slug-based resolution
        if (tenantId == null)
        {
            var tenantSlug = context.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantSlug))
            {
                var tenant = await tenantRepository.GetBySlugAsync(tenantSlug);
                if (tenant != null)
                {
                    tenantId = tenant.Id;
                }
            }
        }

        // Store resolved tenant ID in HttpContext items for downstream use
        if (tenantId.HasValue)
        {
            context.Items["TenantId"] = tenantId.Value;
            _logger.LogDebug("Resolved tenant {TenantId} for request {Path}", tenantId.Value, context.Request.Path);
        }

        if (tenantId.HasValue)
        {
            using (LogContext.PushProperty("TenantId", tenantId.Value))
                await _next(context);
            return;
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }
}
