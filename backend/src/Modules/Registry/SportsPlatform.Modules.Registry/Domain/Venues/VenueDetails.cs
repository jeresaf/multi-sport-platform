namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>
/// A venue's editable fields (S07 D3), trimmed and checked by the use case: the request's shape by its validator, the
/// variants against the published definitions, the zone and country against the reference data. A value out of
/// bounds here is a bug. Surfaces and variants are distinct and kept in the order sent.
/// </summary>
public sealed record VenueDetails(
    string Name,
    string? ShortName,
    VenueType Type,
    string? AddressLine,
    string City,
    string? Region,
    string Country,
    GeoPoint? Location,
    string TimeZone,
    int? Capacity,
    IReadOnlyList<VenueSurface> Surfaces,
    IReadOnlyList<string> SupportedVariants)
{
    public const int NameMaxLength = 120;
    public const int ShortNameMaxLength = 40;
    public const int AddressLineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int RegionMaxLength = 100;
    public const int TimeZoneMaxLength = 64;
    public const int MaxCapacity = 200_000;
    public const int MaxSurfaces = 5;
    public const int MaxSupportedVariants = 20;
    public const int VariantMaxLength = 64;
}
