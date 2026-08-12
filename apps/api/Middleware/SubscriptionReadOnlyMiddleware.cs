using System.Net;
using System.Text;
using System.Text.Json;
using SportsPlatform.Infrastructure.Services;

namespace SportsPlatform.Api.Middleware;

/// <summary>
/// Middleware that enforces read-only mode for tenants whose subscriptions
/// are in ReadOnly or Archived status. Blocks write operations (POST, PUT, PATCH, DELETE)
/// and returns a 403 Forbidden response with a clear message.
/// </summary>
public class SubscriptionReadOnlyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionReadOnlyMiddleware> _logger;

    // HTTP methods that modify data
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "PATCH",
        "DELETE"
    };

    // Paths that are exempt from read-only enforcement
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/subscriptions",
        "/health",
        "/swagger",
    };

    public SubscriptionReadOnlyMiddleware(
        RequestDelegate next,
        ILogger<SubscriptionReadOnlyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISubscriptionsService subscriptionsService)
    {
        var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;

        // Skip exempt paths and non-write methods
        if (!WriteMethods.Contains(context.Request.Method) || IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from the request
        var tenantId = GetTenantIdFromContext(context);
        if (tenantId == null)
        {
            await _next(context);
            return;
        }

        SportsPlatform.Modules.Subscriptions.Entities.Subscription? subscription;
        try
        {
            subscription = await subscriptionsService.GetActiveSubscriptionAsync(tenantId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking subscription status for tenant {TenantId}", tenantId);
            // If we can't verify subscription status, allow the request to proceed
            // to avoid blocking legitimate traffic due to transient errors
            subscription = null;
        }

        if (subscription is not null && !subscription.CanWrite())
        {
            _logger.LogWarning(
                "Write operation blocked for tenant {TenantId} on {Method} {Path}. Subscription status: {Status}",
                tenantId, context.Request.Method, path, subscription.Status);

            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "SubscriptionInReadOnlyMode",
                message = "Your subscription is in read-only mode. Write operations are not allowed.",
                detail = GetReadOnlyDetail(subscription.Status.ToString()),
                status = subscription.Status.ToString(),
                supportContact = "support@sportsplatform.com"
            };

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json, Encoding.UTF8);
            return;
        }

        await _next(context);
    }

    private static Guid? GetTenantIdFromContext(HttpContext context)
    {
        // Try to get tenant ID from the resolved tenant context
        if (context.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is Guid tenantId)
        {
            return tenantId;
        }

        // Fallback: try to parse from X-Tenant-Id header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue) &&
            Guid.TryParse(headerValue, out var headerTenantId))
        {
            return headerTenantId;
        }

        return null;
    }

    private static bool IsExemptPath(string path)
    {
        foreach (var exemptPath in ExemptPaths)
        {
            if (path.StartsWith(exemptPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetReadOnlyDetail(string status)
    {
        return status switch
        {
            "ReadOnly" => "Your subscription has expired and is now in read-only mode. " +
                          "Please renew your subscription to regain full access.",
            "Archived" => "Your subscription has been archived. " +
                          "Please contact support to restore access.",
            _ => "Your subscription is currently restricted. Please check your subscription status."
        };
    }
}

/// <summary>
/// Extension method for registering the SubscriptionReadOnlyMiddleware.
/// </summary>
public static class SubscriptionReadOnlyMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionReadOnlyMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubscriptionReadOnlyMiddleware>();
    }
}
