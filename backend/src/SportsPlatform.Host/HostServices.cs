using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Host.Modules;

namespace SportsPlatform.Host;

/// <summary>Services shared by the API and the CLI.</summary>
public static class HostServices
{
    public static IServiceCollection AddSportsPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        // No users yet (S03 adds the authenticated actor); all work is attributed to the system actor.
        services.AddSingleton<ICurrentActor>(SystemActor.Instance);
        services.AddModules(configuration, ModuleRegistry.All);
        return services;
    }
}
