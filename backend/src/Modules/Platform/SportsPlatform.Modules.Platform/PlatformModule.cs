using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;

namespace SportsPlatform.Modules.Platform;

/// <summary>Cross-cutting foundations: reference data, audit, seed bookkeeping (docs/architecture/modules.md).</summary>
public sealed class PlatformModule : IModule
{
    public string Name => "Platform";

    public string Schema => "platform";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
    }
}
