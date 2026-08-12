using System.Net;
using System.Text.Json;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Api.Middleware;

/// <summary>
/// Prevents tenant-scoped identities from changing protected data while their
/// tenant is suspended or archived. Platform identities carry no tenant claim,
/// so they retain the ability to restore a suspended tenant.
/// </summary>
public sealed class TenantLifecycleMiddleware
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    private static readonly string[] ExemptPaths =
    {
        "/api/auth/login", "/api/auth/refresh", "/api/auth/logout", "/health"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantLifecycleMiddleware> _logger;

    public TenantLifecycleMiddleware(RequestDelegate next, ILogger<TenantLifecycleMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantRepository tenantRepository)
    {
        var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        if (!WriteMethods.Contains(context.Request.Method)
            || ExemptPaths.Any(exempt => path.StartsWith(exempt, StringComparison.OrdinalIgnoreCase))
            || context.User.Identity?.IsAuthenticated != true
            || !context.Items.TryGetValue("TenantId", out var value)
            || value is not Guid tenantId)
        {
            await _next(context);
            return;
        }

        var tenant = await tenantRepository.GetByIdAsync(tenantId, context.RequestAborted);
        if (tenant is null || tenant.Status == TenantStatus.Active)
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Blocked {Method} {Path} for {Status} tenant {TenantId}",
            context.Request.Method, path, tenant.Status, tenantId);
        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = $"Tenant{tenant.Status}",
            message = tenant.Status == TenantStatus.Suspended
                ? "This tenant is suspended. Protected write operations are disabled."
                : "This tenant is archived. Protected write operations are disabled.",
            status = tenant.Status.ToString()
        }), context.RequestAborted);
    }
}

public static class TenantLifecycleMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantLifecycleMiddleware(this IApplicationBuilder builder) =>
        builder.UseMiddleware<TenantLifecycleMiddleware>();
}
