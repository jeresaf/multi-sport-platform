using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SportsPlatform.BuildingBlocks.Modules;

/// <summary>A business module registered explicitly by the Host.</summary>
public interface IModule
{
    /// <summary>Display name, e.g. <c>Platform</c>.</summary>
    string Name { get; }

    /// <summary>The module's PostgreSQL schema, e.g. <c>platform</c>.</summary>
    string Schema { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(ApiEndpointGroups groups);
}
