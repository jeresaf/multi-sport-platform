using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Host.Authorization;

public static class ActionAuthorization
{
    /// <summary>The default evaluator denies; S03 replaces it, tests substitute their own.</summary>
    public static IServiceCollection AddActionAuthorization(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuthorizationEvaluator, DenyAllAuthorizationEvaluator>();
        return services;
    }

    /// <summary>After <c>UseRouting</c>, so the matched endpoint's metadata is known.</summary>
    public static IApplicationBuilder UseActionAuthorization(this IApplicationBuilder app) =>
        app.UseMiddleware<ActionAuthorizationMiddleware>();
}
