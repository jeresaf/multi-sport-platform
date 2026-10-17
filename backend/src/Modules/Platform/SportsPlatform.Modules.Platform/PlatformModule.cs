using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform;

/// <summary>Cross-cutting foundations: reference data, audit, seed bookkeeping (docs/architecture/modules.md).</summary>
public sealed class PlatformModule : IModule
{
    public const string SchemaName = "platform";

    public string Name => "Platform";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<PlatformDbContext>(configuration, Schema);

    public void MapEndpoints(ApiEndpointGroups groups)
    {
    }
}
