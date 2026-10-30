using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.Modules.SportsConfiguration;

/// <summary>
/// Sports Configuration: sport definitions, scoring engines, formats and generators, ruleset templates and the
/// resolver (S05; docs/architecture/modules.md). Global configuration only: no tenant data.
/// </summary>
public sealed class SportsConfigurationModule : IModule
{
    public const string SchemaName = "config";

    public string Name => "SportsConfiguration";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SportDefinitionFiles>();
        // A definition the validator refuses stops the Host before it serves anything (S05 D3).
        services.AddHostedService<SportDefinitionStartupCheck>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
    }
}
