using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Host.Actors;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;

namespace SportsPlatform.Host;

/// <summary>Services shared by the API and the CLI.</summary>
public static class HostServices
{
    public static IServiceCollection AddSportsPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddCurrentActor();
        services.AddModules(configuration, ModuleRegistry.All);

        // What authorization reads besides the roles: the declared actions and entitlements, and the modules' scope
        // chains (S03-T5, S04).
        services.AddSingleton<IActionCatalogue>(new DeclaredActions(ModuleRegistry.All));
        services.AddSingleton<IEntitlementCatalogue>(new DeclaredEntitlements(ModuleRegistry.All));
        services.AddScoped<ScopeChains>();
        return services;
    }
}
