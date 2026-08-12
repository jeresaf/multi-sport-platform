using System.Diagnostics;
using SportsPlatform.Api.Services;

namespace SportsPlatform.Api.Middleware;

public sealed class OperationalMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public OperationalMetricsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, OperationalMetrics metrics)
    {
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        catch
        {
            Record(context, metrics, started, StatusCodes.Status500InternalServerError);
            throw;
        }
        Record(context, metrics, started, context.Response.StatusCode);
    }

    private static void Record(
        HttpContext context,
        OperationalMetrics metrics,
        long started,
        int statusCode)
    {
        var tenantId = context.Items.TryGetValue("TenantId", out var resolved)
            && resolved is Guid resolvedTenantId
                ? resolvedTenantId
                : Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var claimTenantId)
                    ? claimTenantId
                    : (Guid?)null;
        metrics.RecordRequest(
            context.Request.Method,
            statusCode,
            tenantId,
            Stopwatch.GetElapsedTime(started).TotalSeconds);
    }
}
