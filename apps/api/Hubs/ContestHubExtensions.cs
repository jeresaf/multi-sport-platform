using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SportsPlatform.Api.Hubs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SignalR hubs.
/// </summary>
public static class ContestHubExtensions
{
    /// <summary>
    /// Adds the ContestHub services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddContestHubServices(this IServiceCollection services)
    {
        services.AddScoped<ContestHubNotifier>();
        return services;
    }

    /// <summary>
    /// Maps the ContestHub endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapContestHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<ContestHub>("/hubs/contest");
        return endpoints;
    }
}
