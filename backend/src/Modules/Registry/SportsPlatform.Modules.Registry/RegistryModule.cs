using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.Aliases;
using SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Features.People.Consents;
using SportsPlatform.Modules.Registry.Features.People.CreatePerson;
using SportsPlatform.Modules.Registry.Features.People.ExternalIdentifiers;
using SportsPlatform.Modules.Registry.Features.People.GetPerson;
using SportsPlatform.Modules.Registry.Features.People.GetPersonPrivate;
using SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;
using SportsPlatform.Modules.Registry.Features.People.ListPeople;
using SportsPlatform.Modules.Registry.Features.People.ListPersonStatusChanges;
using SportsPlatform.Modules.Registry.Features.People.Photo;
using SportsPlatform.Modules.Registry.Features.People.Profiles;
using SportsPlatform.Modules.Registry.Features.People.PublicProfile;
using SportsPlatform.Modules.Registry.Features.People.UpdatePerson;
using SportsPlatform.Modules.Registry.Features.Venues;
using SportsPlatform.Modules.Registry.Features.Venues.ChangeVenueStatus;
using SportsPlatform.Modules.Registry.Features.Venues.CheckVenueDuplicates;
using SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenues;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenueStatusChanges;
using SportsPlatform.Modules.Registry.Features.Venues.UpdateVenue;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Seed.Demo;

namespace SportsPlatform.Modules.Registry;

/// <summary>
/// Registry: the canonical records of people (S06), then organisations, teams and venues (S07)
/// (docs/architecture/modules.md). Every table is tenant-owned. S06-T1b: the people reads and
/// <see cref="IPeopleDirectory"/>. S06-T2a: the duplicate check. S06-T2b: its token and create. S06-T3a: the
/// update and the lifecycle. S06-T3b: aliases and external identifiers. S06-T4a: athlete profiles. S06-T4b1: staff profiles.
/// S06-T4b2: official profiles.
/// S06-T5b: photos. S06-T6a: guardian consents and their evidence. S06-T7d: the demo people. S07-T1a: venues (create
/// with the duplicate warning, the read). S07-T1b-1: the venue's update, lifecycle and status history.
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
        services.AddScoped<SetPersonPhotoHandler>();
        services.AddScoped<RemovePersonPhotoHandler>();
        services.AddScoped<RecordConsentHandler>();
        services.AddScoped<WithdrawConsentHandler>();
        services.AddScoped<UploadConsentEvidenceHandler>();
        services.AddScoped<EnablePublicProfileHandler>();
        services.AddScoped<DisablePublicProfileHandler>();
        services.AddScoped<PublicPeople>();
        services.AddScoped<GetPublicPersonHandler>();
        services.AddScoped<GetPublicPersonPhotoHandler>();
        services.AddScoped<AddExternalIdentifierHandler>();
        services.AddScoped<EndExternalIdentifierHandler>();
        services.AddScoped<SportCodes>();
        services.AddScoped<CreateAthleteProfileHandler>();
        services.AddScoped<UpdateAthleteProfileHandler>();
        services.AddScoped<ChangeAthleteProfileStatusHandler>();
        services.AddScoped<CreateStaffProfileHandler>();
        services.AddScoped<UpdateStaffProfileHandler>();
        services.AddScoped<ChangeStaffProfileStatusHandler>();
        services.AddScoped<CreateOfficialProfileHandler>();
        services.AddScoped<UpdateOfficialProfileHandler>();
        services.AddScoped<ChangeOfficialProfileStatusHandler>();
        services.AddScoped<VenueDetailsRules>();
        services.AddScoped<VenueDuplicateFinder>();
        services.AddScoped<CheckVenueDuplicatesHandler>();
        services.AddScoped<CreateVenueHandler>();
        services.AddScoped<GetVenueHandler>();
        services.AddScoped<ListVenuesHandler>();
        services.AddScoped<UpdateVenueHandler>();
        services.AddScoped<ChangeVenueStatusHandler>();
        services.AddScoped<ListVenueStatusChangesHandler>();
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
        services.AddSingleton<IValidator<OfficialProfileRequest>, OfficialProfileRequestValidator>();
        services.AddSingleton<IValidator<ChangeProfileStatusRequest>, ChangeProfileStatusRequestValidator>();
        services.AddSingleton<IValidator<RecordConsentRequest>, RecordConsentRequestValidator>();
        services.AddSingleton<IValidator<WithdrawConsentRequest>, WithdrawConsentRequestValidator>();
        services.AddSingleton<IValidator<PublicPersonPhotoRequest>, PublicPersonPhotoRequestValidator>();
        services.AddSingleton<IValidator<CheckVenueDuplicatesRequest>, CheckVenueDuplicatesRequestValidator>();
        services.AddScoped<IValidator<CreateVenueRequest>, CreateVenueRequestValidator>();
        services.AddScoped<IValidator<UpdateVenueRequest>, UpdateVenueRequestValidator>();
        services.AddSingleton<IValidator<ChangeVenueStatusRequest>, ChangeVenueStatusRequestValidator>();
        services.AddSingleton<IValidator<ListVenuesRequest>, ListVenuesRequestValidator>();

        services.AddSeeder<DemoPeopleSeeder>();
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
        PersonPhotoEndpoints.Map(people);
        ConsentEndpoints.Map(people);
        PublicProfileEndpoints.Map(people);
        ExternalIdentifierEndpoints.Map(people);
        AthleteProfileEndpoints.Map(people);
        StaffProfileEndpoints.Map(people);
        OfficialProfileEndpoints.Map(people);
        GetPersonPrivateEndpoint.Map(people);
        ListPersonStatusChangesEndpoint.Map(people);

        var venues = VenuesEndpoints.Group(groups.Admin);
        CheckVenueDuplicatesEndpoint.Map(venues);
        CreateVenueEndpoint.Map(venues);
        ListVenuesEndpoint.Map(venues);
        GetVenueEndpoint.Map(venues);
        UpdateVenueEndpoint.Map(venues);
        ChangeVenueStatusEndpoint.Map(venues);
        ListVenueStatusChangesEndpoint.Map(venues);

        PublicPersonEndpoints.Map(groups.Public);
    }
}
