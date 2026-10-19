using SportsPlatform.Host.Actors;
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
        return services;
    }
}
