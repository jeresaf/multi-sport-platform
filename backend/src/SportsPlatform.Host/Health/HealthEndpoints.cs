using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SportsPlatform.Host.Health;

/// <summary>
/// <c>GET /health/live</c> (the process answers) and <c>GET /health/ready</c> (the database answers). Both return only
/// <c>{"status": …}</c>: no versions, host names or error text.
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
        endpoints.MapGet("/health/live", () => TypedResults.Ok(new HealthResponse("ok")))
            .WithName("HealthLive");

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
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
        }).WithName("HealthReady");

        return endpoints;
    }

    public static bool IsHealthRequest(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HealthResponse(string Status);
}
