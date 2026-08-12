using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace SportsPlatform.Api.Middleware;

public sealed class ObservabilityContextMiddleware
{
    private readonly RequestDelegate _next;

    public ObservabilityContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.Items.TryGetValue("TenantId", out var tenant)
            ? tenant?.ToString()
            : null;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!string.IsNullOrWhiteSpace(tenantId))
            Activity.Current?.SetTag("tenant.id", tenantId);
        if (!string.IsNullOrWhiteSpace(userId))
            Activity.Current?.SetTag("enduser.id", userId);

        using (LogContext.PushProperty("TenantId", tenantId ?? "anonymous"))
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
            await _next(context);
    }
}
