using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Media;

namespace SportsPlatform.Modules.Registry.Features.Organisations.PublicOrganisations;

/// <summary>
/// An organisation on the public site (S07 D17): its <c>PUBLIC</c> fields only (D16; no legal name, email, phone, aliases
/// or identifiers). <c>primaryVenue</c> and <c>parent</c> are null without one or when that record is archived; children
/// and teams leave archived ones out. <c>logo</c> holds the public asset route's paths.
/// </summary>
public sealed record PublicOrganisationResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationType Type,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] string? City,
    [property: PersonalData(PersonalDataClass.Public)] int? FoundedYear,
    [property: PersonalData(PersonalDataClass.Public)] string? Website,
    [property: PersonalData(PersonalDataClass.Public)] string? Description,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationVenueResponse? PrimaryVenue,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationSummaryResponse? Parent,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<OrganisationSummaryResponse> Children,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<OrganisationTeamResponse> Teams,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] PublicImageResponse? Logo);

/// <summary>An organisation in the public directory (S07 D17): the admin directory's fields and the logo's paths.</summary>
public sealed record PublicOrganisationListItem(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationType Type,
    [property: PersonalData(PersonalDataClass.Public)] string? City,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] PublicImageResponse? Logo);

// The cursor carries only a sort key built from the public display name and an ID. A plain comment: a summary would
// reach OpenAPI.
public sealed record ListPublicOrganisationsResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<PublicOrganisationListItem> Items,
    [property: PersonalData(PersonalDataClass.Public)] PublicPageInfo Page);
