using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Contracts.Organisations;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Contracts.Venues;
using SportsPlatform.Modules.Registry.Features.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.Aliases;
using SportsPlatform.Modules.Registry.Features.Organisations.ChangeOrganisationStatus;
using SportsPlatform.Modules.Registry.Features.Organisations.CheckOrganisationDuplicates;
using SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;
using SportsPlatform.Modules.Registry.Features.Organisations.ExternalIdentifiers;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;
using SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisationParentLinks;
using SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisations;
using SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisationStatusChanges;
using SportsPlatform.Modules.Registry.Features.Organisations.SetOrganisationParent;
using SportsPlatform.Modules.Registry.Features.Organisations.UpdateOrganisation;
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
using SportsPlatform.Modules.Registry.Features.Teams;
using SportsPlatform.Modules.Registry.Features.Teams.Aliases;
using SportsPlatform.Modules.Registry.Features.Teams.ChangeTeamStatus;
using SportsPlatform.Modules.Registry.Features.Teams.CheckTeamDuplicates;
using SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;
using SportsPlatform.Modules.Registry.Features.Teams.ListTeams;
using SportsPlatform.Modules.Registry.Features.Teams.ListTeamStatusChanges;
using SportsPlatform.Modules.Registry.Features.Teams.RenameTeam;
using SportsPlatform.Modules.Registry.Features.Teams.UpdateTeam;
using SportsPlatform.Modules.Registry.Features.Venues;
using SportsPlatform.Modules.Registry.Features.Venues.Aliases;
using SportsPlatform.Modules.Registry.Features.Venues.ChangeVenueStatus;
using SportsPlatform.Modules.Registry.Features.Venues.CheckVenueDuplicates;
using SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;
using SportsPlatform.Modules.Registry.Features.Venues.ExternalIdentifiers;
using SportsPlatform.Modules.Registry.Features.Venues.GetVenue;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenues;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenueStatusChanges;
using SportsPlatform.Modules.Registry.Features.Venues.UpdateVenue;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Seed.Demo;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry;

/// <summary>
/// Registry: the canonical records of people (S06), then organisations, teams and venues (S07)
/// (docs/architecture/modules.md). Every table is tenant-owned. S06-T1b: the people reads and
/// <see cref="IPeopleDirectory"/>. S06-T2a: the duplicate check. S06-T2b: its token and create. S06-T3a: the
/// update and the lifecycle. S06-T3b: aliases and external identifiers. S06-T4a: athlete profiles. S06-T4b1: staff profiles.
/// S06-T4b2: official profiles.
/// S06-T5b: photos. S06-T6a: guardian consents and their evidence. S06-T7d: the demo people. S07-T1a: venues (create
/// with the duplicate warning, the read). S07-T1b-1: the venue's update, lifecycle and status history.
/// S07-T1b-2: the venue directory. S07-T1c: venue aliases and external identifiers, <see cref="IVenueDirectory"/>.
/// S07-T2a: organisations (create with the duplicate warning, the read). S07-T2b and T2c: their update, lifecycle,
/// directory and parent links. S07-T2d: organisation aliases and external identifiers, <see cref="IOrganisationDirectory"/>.
/// S07-T3a: teams (create with the duplicate warning and <c>MAX_TEAMS</c>, the read).
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
        services.AddScoped<OrganisationDetailsRules>();
        services.AddScoped<OrganisationDuplicateFinder>();
        services.AddScoped<CheckOrganisationDuplicatesHandler>();
        services.AddScoped<CreateOrganisationHandler>();
        services.AddScoped<GetOrganisationHandler>();
        services.AddScoped<UpdateOrganisationHandler>();
        services.AddScoped<ChangeOrganisationStatusHandler>();
        services.AddScoped<ListOrganisationsHandler>();
        services.AddScoped<ListOrganisationStatusChangesHandler>();
        services.AddScoped<SetOrganisationParentHandler>();
        services.AddScoped<ListOrganisationParentLinksHandler>();
        services.AddScoped<AddOrganisationAliasHandler>();
        services.AddScoped<RemoveOrganisationAliasHandler>();
        services.AddScoped<AddOrganisationExternalIdentifierHandler>();
        services.AddScoped<EndOrganisationExternalIdentifierHandler>();
        services.AddScoped<IOrganisationDirectory, OrganisationDirectory>();
        services.AddScoped<TeamDetailsRules>();
        services.AddScoped<TeamDuplicateFinder>();
        services.AddScoped<CheckTeamDuplicatesHandler>();
        services.AddScoped<CreateTeamHandler>();
        services.AddScoped<GetTeamHandler>();
        services.AddScoped<UpdateTeamHandler>();
        services.AddScoped<ChangeTeamStatusHandler>();
        services.AddScoped<ListTeamStatusChangesHandler>();
        services.AddScoped<ListTeamsHandler>();
        services.AddScoped<RenameTeamHandler>();
        services.AddScoped<AddTeamAliasHandler>();
        services.AddScoped<RemoveTeamAliasHandler>();
        services.AddQuotaUsage<TeamCount>(TeamCount.Code);
        services.AddScoped<VenueDetailsRules>();
        services.AddScoped<VenueDuplicateFinder>();
        services.AddScoped<CheckVenueDuplicatesHandler>();
        services.AddScoped<CreateVenueHandler>();
        services.AddScoped<GetVenueHandler>();
        services.AddScoped<ListVenuesHandler>();
        services.AddScoped<UpdateVenueHandler>();
        services.AddScoped<ChangeVenueStatusHandler>();
        services.AddScoped<ListVenueStatusChangesHandler>();
        services.AddScoped<AddVenueAliasHandler>();
        services.AddScoped<RemoveVenueAliasHandler>();
        services.AddScoped<AddVenueExternalIdentifierHandler>();
        services.AddScoped<EndVenueExternalIdentifierHandler>();
        services.AddScoped<IVenueDirectory, VenueDirectory>();
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
        services.AddSingleton<IValidator<CheckOrganisationDuplicatesRequest>, CheckOrganisationDuplicatesRequestValidator>();
        services.AddScoped<IValidator<CreateOrganisationRequest>, CreateOrganisationRequestValidator>();
        services.AddScoped<IValidator<UpdateOrganisationRequest>, UpdateOrganisationRequestValidator>();
        services.AddSingleton<IValidator<ChangeOrganisationStatusRequest>, ChangeOrganisationStatusRequestValidator>();
        services.AddSingleton<IValidator<SetOrganisationParentRequest>, SetOrganisationParentRequestValidator>();
        services.AddSingleton<IValidator<AddOrganisationAliasRequest>, AddOrganisationAliasRequestValidator>();
        services.AddSingleton<IValidator<RemoveOrganisationAliasRequest>, RemoveOrganisationAliasRequestValidator>();
        services.AddSingleton<IValidator<AddOrganisationExternalIdentifierRequest>, AddOrganisationExternalIdentifierRequestValidator>();
        services.AddSingleton<IValidator<EndOrganisationExternalIdentifierRequest>, EndOrganisationExternalIdentifierRequestValidator>();
        services.AddSingleton<IValidator<CheckTeamDuplicatesRequest>, CheckTeamDuplicatesRequestValidator>();
        services.AddSingleton<IValidator<CreateTeamRequest>, CreateTeamRequestValidator>();
        services.AddSingleton<IValidator<UpdateTeamRequest>, UpdateTeamRequestValidator>();
        services.AddSingleton<IValidator<ChangeTeamStatusRequest>, ChangeTeamStatusRequestValidator>();
        services.AddSingleton<IValidator<CheckVenueDuplicatesRequest>, CheckVenueDuplicatesRequestValidator>();
        services.AddScoped<IValidator<CreateVenueRequest>, CreateVenueRequestValidator>();
        services.AddScoped<IValidator<UpdateVenueRequest>, UpdateVenueRequestValidator>();
        services.AddSingleton<IValidator<ChangeVenueStatusRequest>, ChangeVenueStatusRequestValidator>();
        services.AddSingleton<IValidator<ListOrganisationsRequest>, ListOrganisationsRequestValidator>();
        services.AddSingleton<IValidator<ListVenuesRequest>, ListVenuesRequestValidator>();
        services.AddSingleton<IValidator<ListTeamsRequest>, ListTeamsRequestValidator>();
        services.AddSingleton<IValidator<RenameTeamRequest>, RenameTeamRequestValidator>();
        services.AddSingleton<IValidator<AddTeamAliasRequest>, AddTeamAliasRequestValidator>();
        services.AddSingleton<IValidator<RemoveTeamAliasRequest>, RemoveTeamAliasRequestValidator>();
        services.AddSingleton<IValidator<AddVenueAliasRequest>, AddVenueAliasRequestValidator>();
        services.AddSingleton<IValidator<RemoveVenueAliasRequest>, RemoveVenueAliasRequestValidator>();
        services.AddSingleton<IValidator<AddVenueExternalIdentifierRequest>, AddVenueExternalIdentifierRequestValidator>();
        services.AddSingleton<IValidator<EndVenueExternalIdentifierRequest>, EndVenueExternalIdentifierRequestValidator>();

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

        var organisations = OrganisationsEndpoints.Group(groups.Admin);
        CheckOrganisationDuplicatesEndpoint.Map(organisations);
        CreateOrganisationEndpoint.Map(organisations);
        ListOrganisationsEndpoint.Map(organisations);
        GetOrganisationEndpoint.Map(organisations);
        UpdateOrganisationEndpoint.Map(organisations);
        ChangeOrganisationStatusEndpoint.Map(organisations);
        ListOrganisationStatusChangesEndpoint.Map(organisations);
        SetOrganisationParentEndpoint.Map(organisations);
        ListOrganisationParentLinksEndpoint.Map(organisations);
        OrganisationAliasEndpoints.Map(organisations);
        OrganisationExternalIdentifierEndpoints.Map(organisations);

        var teams = TeamsEndpoints.Group(groups.Admin);
        CheckTeamDuplicatesEndpoint.Map(teams);
        CreateTeamEndpoint.Map(teams);
        ListTeamsEndpoint.Map(teams);
        GetTeamEndpoint.Map(teams);
        UpdateTeamEndpoint.Map(teams);
        ChangeTeamStatusEndpoint.Map(teams);
        ListTeamStatusChangesEndpoint.Map(teams);
        RenameTeamEndpoint.Map(teams);
        TeamAliasEndpoints.Map(teams);

        var venues = VenuesEndpoints.Group(groups.Admin);
        CheckVenueDuplicatesEndpoint.Map(venues);
        CreateVenueEndpoint.Map(venues);
        ListVenuesEndpoint.Map(venues);
        GetVenueEndpoint.Map(venues);
        UpdateVenueEndpoint.Map(venues);
        ChangeVenueStatusEndpoint.Map(venues);
        ListVenueStatusChangesEndpoint.Map(venues);
        VenueAliasEndpoints.Map(venues);
        VenueExternalIdentifierEndpoints.Map(venues);

        PublicPersonEndpoints.Map(groups.Public);
    }
}
