using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Features.People.CreatePerson;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;
using SportsPlatform.Modules.Registry.Features.People.GetPersonPrivate;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Features.People.ListPersonStatusChanges;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry;

/// <summary>
/// Registry: the canonical records of people (S06), then organisations, teams and venues (S07)
/// (docs/architecture/modules.md). Every table is tenant-owned. S06-T1b: the people reads and
/// <see cref="IPeopleDirectory"/>. S06-T2a: the duplicate check. S06-T2b: its token and create.
/// </summary>
public sealed class RegistryModule : IModule
{
    public const string SchemaName = "registry";

    public string Name => "Registry";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<RegistryDbContext>(configuration, Schema);

        services.AddScoped<TenantToday>();
        services.AddScoped<ListPeopleHandler>();
        services.AddScoped<GetPersonHandler>();
        services.AddScoped<GetPersonPrivateHandler>();
        services.AddScoped<ListPersonStatusChangesHandler>();
        services.AddScoped<IPeopleDirectory, PeopleDirectory>();
        services.AddScoped<DuplicateFinder>();
        services.AddScoped<DuplicateCheckTokens>();
        services.AddScoped<CheckDuplicatesHandler>();
        services.AddScoped<CreatePersonHandler>();
        services.AddSingleton<IValidator<ListPeopleRequest>, ListPeopleRequestValidator>();
        services.AddScoped<IValidator<CheckDuplicatesRequest>, CheckDuplicatesRequestValidator>();
        services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var people = PeopleEndpoints.Group(groups.Admin);
        ListPeopleEndpoint.Map(people);
        CheckDuplicatesEndpoint.Map(people);
        CreatePersonEndpoint.Map(people);
        GetPersonEndpoint.Map(people);
        GetPersonPrivateEndpoint.Map(people);
        ListPersonStatusChangesEndpoint.Map(people);
    }
}
