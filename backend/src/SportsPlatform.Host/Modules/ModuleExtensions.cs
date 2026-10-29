using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Host.OpenApi;

namespace SportsPlatform.Host.Modules;

public static class ModuleExtensions
{
    public const string PublicPrefix = "/api/public/v1";
    public const string AdminPrefix = "/api/admin/v1";
    public const string PlatformPrefix = "/api/platform/v1";
    public const string AccountPrefix = "/api/account/v1";

    public static IServiceCollection AddModules(
        this IServiceCollection services, IConfiguration configuration, IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
        {
            module.AddServices(services, configuration);
        }

        return services;
    }

    /// <summary>
    /// Maps each module's endpoints into the four API families. Public endpoints are <c>.AllowPublic()</c> as a group, so
    /// one that also requires an action is rejected at startup. Admin, platform and account endpoints document the
    /// pipeline's 401s and 403s; account responses are never stored by a cache (S03 D1).
    /// </summary>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints, IEnumerable<IModule> modules)
    {
        var entitlements = endpoints.ServiceProvider.GetRequiredService<IEntitlementCatalogue>();
        var groups = new ApiEndpointGroups(
            Public: endpoints.MapGroup(PublicPrefix).AllowPublic(),
            Admin: endpoints.MapGroup(AdminPrefix).DocumentPipelineErrors(entitlements),
            Platform: endpoints.MapGroup(PlatformPrefix).DocumentPipelineErrors(entitlements),
            Account: endpoints.MapGroup(AccountPrefix).DocumentPipelineErrors(entitlements).AddEndpointFilter(static (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return next(context);
            }));

        foreach (var module in modules)
        {
            module.MapEndpoints(groups);
        }

        return endpoints;
    }
}
