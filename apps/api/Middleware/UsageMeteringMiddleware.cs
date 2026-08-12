using SportsPlatform.Infrastructure.Services;

namespace SportsPlatform.Api.Middleware;

/// <summary>
/// Records authenticated tenant API volume in durable hourly buckets. Metering is
/// best-effort so an unavailable metrics store never turns a valid request into a 500.
/// </summary>
public sealed class UsageMeteringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UsageMeteringMiddleware> _logger;

    public UsageMeteringMiddleware(RequestDelegate next, ILogger<UsageMeteringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUsageLimitsService usageLimits)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            var tenantValue = context.User.FindFirst("tenant_id")?.Value;
            if (context.Request.Path.StartsWithSegments("/api") &&
                context.User.Identity?.IsAuthenticated == true &&
                Guid.TryParse(tenantValue, out var tenantId))
            {
                try
                {
                    await usageLimits.IncrementUsageAsync(
                        tenantId,
                        UsageLimitsService.ApiRequestsMetric,
                        cancellationToken: CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Unable to meter API usage for tenant {TenantId}", tenantId);
                }
            }
        }
    }
}
