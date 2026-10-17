using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Host.Health;

/// <summary>
/// <c>GET /health/live</c> (the process answers) and <c>GET /health/ready</c> (the database answers). Both return only
/// <c>{"status": …}</c>: no versions, host names or error text. Probes are infrastructure, not part of the API
/// contract, so they are left out of the OpenAPI document.
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    private static readonly TimeSpan _checkTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddHealthEndpoints(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag], timeout: _checkTimeout);
        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(LivePath, () => TypedResults.Ok(new HealthResponse("ok")))
            .WithName("HealthLive")
            .ExcludeFromDescription()
            .AllowPublic();

        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
            ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(
                new HealthResponse(report.Status == HealthStatus.Unhealthy ? "unavailable" : "ok"), context.RequestAborted),
        }).WithName("HealthReady")
            .ExcludeFromDescription()
            .AllowPublic();

        return endpoints;
    }

    public const string LivePath = "/health/live";

    public const string ReadyPath = "/health/ready";

    /// <summary>The two probe paths exactly; anything else under <c>/health</c> is an ordinary (traced) request.</summary>
    public static bool IsHealthRequest(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = context.Request.Path;
        return path.Equals(LivePath, StringComparison.OrdinalIgnoreCase) || path.Equals(ReadyPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HealthResponse(string Status);
}
