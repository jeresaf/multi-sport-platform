using SportsPlatform.BuildingBlocks.Modules;

namespace SportsPlatform.Host.Modules;

public static class ModuleExtensions
{
    public const string PublicPrefix = "/api/public/v1";
    public const string AdminPrefix = "/api/admin/v1";
    public const string PlatformPrefix = "/api/platform/v1";

    public static IServiceCollection AddModules(
        this IServiceCollection services, IConfiguration configuration, IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.AddServices(services, configuration);
        }

        return services;
    }

    /// <summary>Maps each module's endpoints into the three API families.</summary>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints, IEnumerable<IModule> modules)
    {
        var groups = new ApiEndpointGroups(
            Public: endpoints.MapGroup(PublicPrefix),
            Admin: endpoints.MapGroup(AdminPrefix),
            Platform: endpoints.MapGroup(PlatformPrefix));

        foreach (var module in modules)
        {
            module.MapEndpoints(groups);
        }

        return endpoints;
    }
}
