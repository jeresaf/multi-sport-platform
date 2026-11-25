using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Venues.ListVenues;

namespace SportsPlatform.Modules.Registry.Features.Venues.PublicVenues;

/// <summary>A venue on the public site (S07 D17): its <c>PUBLIC</c> fields only (D16; no aliases or identifiers).</summary>
public sealed record PublicVenueResponse(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string Name,
    [property: PersonalData(PersonalDataClass.Public)] string? ShortName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] VenueType Type,
    [property: PersonalData(PersonalDataClass.Public)] string? AddressLine,
    [property: PersonalData(PersonalDataClass.Public)] string City,
    [property: PersonalData(PersonalDataClass.Public)] string? Region,
    [property: PersonalData(PersonalDataClass.Public)] string Country,
    [property: PersonalData(PersonalDataClass.Public)] decimal? Latitude,
    [property: PersonalData(PersonalDataClass.Public)] decimal? Longitude,
    [property: PersonalData(PersonalDataClass.Public)] string TimeZone,
    [property: PersonalData(PersonalDataClass.Public)] int? Capacity,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<VenueSurface> Surfaces,
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<string> SupportedVariants,
    [property: PersonalData(PersonalDataClass.Public)] VenueStatus Status);

/// <summary>
/// The public venue directory's page (S07 D17): the admin directory's items (every field is public, D16) and a public
/// page whose cursor carries only the name's sort key and an ID.
/// </summary>
public sealed record ListPublicVenuesResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<VenueListItem> Items,
    [property: PersonalData(PersonalDataClass.Public)] PublicPageInfo Page);
