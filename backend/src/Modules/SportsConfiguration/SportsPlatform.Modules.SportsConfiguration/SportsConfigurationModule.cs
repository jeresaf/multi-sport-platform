using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Engines;
using SportsPlatform.Modules.SportsConfiguration.Features.Sports;
using SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;
using SportsPlatform.Modules.SportsConfiguration.Features.Sports.ListSports;
using SportsPlatform.Modules.SportsConfiguration.Infrastructure;
using SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

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
        services.AddModuleDbContext<SportsConfigurationDbContext>(configuration, Schema);
        services.AddSingleton<SportDefinitionFiles>();
        // A definition the validator refuses stops the Host before it serves anything (S05 D3).
        services.AddHostedService<SportDefinitionStartupCheck>();
        services.AddSeeder<SportDefinitionSeeder>();
        services.AddSingleton<SportDefinitionCache>();
        services.AddScoped<ISportDefinitions, SportDefinitions>();
        services.AddSingleton<IScoringEngine, AdditiveEngine>();
        services.AddSingleton<IScoringEngines, ScoringEngines>();
        services.AddScoped<ListSportsHandler>();
        services.AddScoped<GetSportHandler>();
        services.AddSingleton<IValidator<GetSportRequest>, GetSportRequestValidator>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var adminSports = SportEndpoints.Group(groups.Admin);
        ListSportsEndpoint.MapAdmin(adminSports);
        GetSportEndpoint.MapAdmin(adminSports);
        var platformSports = SportEndpoints.Group(groups.Platform);
        ListSportsEndpoint.MapPlatform(platformSports);
        GetSportEndpoint.MapPlatform(platformSports);
    }
}
