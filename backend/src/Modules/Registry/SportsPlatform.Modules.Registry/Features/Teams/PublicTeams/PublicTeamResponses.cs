using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.Media;

namespace SportsPlatform.Modules.Registry.Features.Teams.PublicTeams;

/// <summary>
/// A team on the public site (S07 D17): its <c>PUBLIC</c> fields only (D16; no nicknames, spellings or identifiers).
/// <c>organisation</c> and <c>homeVenue</c> are null without one or when that record is archived. <c>formerNames</c> are
/// the earlier names with their dates, newest first. <c>logo</c> and <c>badge</c> hold the public asset route's paths.
/// </summary>
public sealed record PublicTeamResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] GenderCategory GenderCategory,
    [property: PersonalData(PersonalDataClass.Public)] AgeGroup AgeGroup,
    [property: PersonalData(PersonalDataClass.Public)] TeamLevel Level,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] DateOnly NameSince,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string? Code,
    [property: PersonalData(PersonalDataClass.Public)] string? Motto,
    [property: PersonalData(PersonalDataClass.Public)] string? PrimaryColour,
    [property: PersonalData(PersonalDataClass.Public)] string? SecondaryColour,
    [property: PersonalData(PersonalDataClass.Public)] string? TertiaryColour,
    [property: PersonalData(PersonalDataClass.Public)] int? FoundedYear,
    [property: PersonalData(PersonalDataClass.Public)] TeamOrganisationResponse? Organisation,
    [property: PersonalData(PersonalDataClass.Public)] TeamVenueResponse? HomeVenue,
    [property: PersonalData(PersonalDataClass.Public)] TeamStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] PublicImageResponse? Logo,
    [property: PersonalData(PersonalDataClass.Public)] PublicImageResponse? Badge,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<TeamFormerNameResponse> FormerNames);

/// <summary>
/// A team in the public directory (S07 D17): the admin directory's fields, its organisation null when archived, and the
/// logo's and badge's paths.
/// </summary>
public sealed record PublicTeamListItem(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string Variant,
    [property: PersonalData(PersonalDataClass.Public)] GenderCategory GenderCategory,
    [property: PersonalData(PersonalDataClass.Public)] AgeGroup AgeGroup,
    [property: PersonalData(PersonalDataClass.Public)] TeamLevel Level,
    [property: PersonalData(PersonalDataClass.Public)] TeamOrganisationResponse? Organisation,
    [property: PersonalData(PersonalDataClass.Public)] TeamStatus Status,
    [property: PersonalData(PersonalDataClass.Public)] PublicImageResponse? Logo,
    [property: PersonalData(PersonalDataClass.Public)] PublicImageResponse? Badge);

// The cursor carries only a sort key built from the public name, a category rank and an ID. A plain comment: a summary
// would reach OpenAPI.
public sealed record ListPublicTeamsResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<PublicTeamListItem> Items,
    [property: PersonalData(PersonalDataClass.Public)] PublicPageInfo Page);
