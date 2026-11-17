using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.Aliases;
using SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Features.People.CreatePerson;
using SportsPlatform.Modules.Registry.Features.People.ExternalIdentifiers;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;
using SportsPlatform.Modules.Registry.Features.People.GetPersonPrivate;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Features.People.ListPersonStatusChanges;
using SportsPlatform.Modules.Registry.Features.People.Profiles;
using SportsPlatform.Modules.Registry.Features.People.UpdatePerson;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry;

/// <summary>
/// Registry: the canonical records of people (S06), then organisations, teams and venues (S07)
/// (docs/architecture/modules.md). Every table is tenant-owned. S06-T1b: the people reads and
/// <see cref="IPeopleDirectory"/>. S06-T2a: the duplicate check. S06-T2b: its token and create. S06-T3a: the
/// update and the lifecycle. S06-T3b: aliases and external identifiers. S06-T4a: athlete profiles. S06-T4b1: staff profiles.
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
        services.AddScoped<UpdatePersonHandler>();
        services.AddScoped<ChangePersonStatusHandler>();
        services.AddScoped<AddPersonAliasHandler>();
        services.AddScoped<RemovePersonAliasHandler>();
        services.AddScoped<AddExternalIdentifierHandler>();
        services.AddScoped<EndExternalIdentifierHandler>();
        services.AddScoped<SportCodes>();
        services.AddScoped<CreateAthleteProfileHandler>();
        services.AddScoped<UpdateAthleteProfileHandler>();
        services.AddScoped<ChangeAthleteProfileStatusHandler>();
        services.AddScoped<CreateStaffProfileHandler>();
        services.AddScoped<UpdateStaffProfileHandler>();
        services.AddScoped<ChangeStaffProfileStatusHandler>();
        services.AddSingleton<IValidator<ListPeopleRequest>, ListPeopleRequestValidator>();
        services.AddScoped<IValidator<CheckDuplicatesRequest>, CheckDuplicatesRequestValidator>();
        services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
        services.AddScoped<IValidator<UpdatePersonRequest>, UpdatePersonRequestValidator>();
        services.AddSingleton<IValidator<ChangePersonStatusRequest>, ChangePersonStatusRequestValidator>();
        services.AddSingleton<IValidator<ChangePersonStatusWithReasonRequest>, ChangePersonStatusWithReasonRequestValidator>();
        services.AddSingleton<IValidator<AddPersonAliasRequest>, AddPersonAliasRequestValidator>();
        services.AddSingleton<IValidator<RemovePersonAliasRequest>, RemovePersonAliasRequestValidator>();
        services.AddSingleton<IValidator<AddExternalIdentifierRequest>, AddExternalIdentifierRequestValidator>();
        services.AddSingleton<IValidator<EndExternalIdentifierRequest>, EndExternalIdentifierRequestValidator>();
        services.AddSingleton<IValidator<CreateAthleteProfileRequest>, CreateAthleteProfileRequestValidator>();
        services.AddSingleton<IValidator<UpdateAthleteProfileRequest>, UpdateAthleteProfileRequestValidator>();
        services.AddSingleton<IValidator<StaffProfileRequest>, StaffProfileRequestValidator>();
        services.AddSingleton<IValidator<ChangeProfileStatusRequest>, ChangeProfileStatusRequestValidator>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var people = PeopleEndpoints.Group(groups.Admin);
        ListPeopleEndpoint.Map(people);
        CheckDuplicatesEndpoint.Map(people);
        CreatePersonEndpoint.Map(people);
        GetPersonEndpoint.Map(people);
        UpdatePersonEndpoint.Map(people);
        ChangePersonStatusEndpoints.Map(people);
        PersonAliasEndpoints.Map(people);
        ExternalIdentifierEndpoints.Map(people);
        AthleteProfileEndpoints.Map(people);
        StaffProfileEndpoints.Map(people);
        GetPersonPrivateEndpoint.Map(people);
        ListPersonStatusChangesEndpoint.Map(people);
    }
}
