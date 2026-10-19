using Microsoft.Extensions.DependencyInjection.Extensions;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Host.Tenancy;

public static class TenantResolution
{
    public static IServiceCollection AddTenantResolution(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantMembershipChecker, NoTenantMemberships>();
        return services;
    }

    /// <summary>After <c>UseAudit</c> and before <c>UseActionAuthorization</c> (S02 D8).</summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
