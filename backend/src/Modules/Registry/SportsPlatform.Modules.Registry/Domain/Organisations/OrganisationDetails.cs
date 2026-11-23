namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>
/// An organisation's editable fields (S07 D4), trimmed and checked by the use case: the request's shape by its validator,
/// the country against the reference data, the founded year against the tenant's year, the primary venue against the
/// tenant's venues. A value out of bounds here is a bug. Blank optional texts are null.
/// </summary>
public sealed record OrganisationDetails(
    OrganisationType Type,
    string DisplayName,
    string? LegalName,
    string? ShortName,
    string Country,
    string? City,
    int? FoundedYear,
    string? Website,
    string? Email,
    string? Phone,
    string? Description,
    Guid? PrimaryVenueId)
{
    public const int DisplayNameMaxLength = 120;
    public const int LegalNameMaxLength = 200;
    public const int ShortNameMaxLength = 40;
    public const int CityMaxLength = 100;
    public const int WebsiteMaxLength = 200;
    public const int EmailMaxLength = 200;
    public const int PhoneMaxLength = 30;
    public const int DescriptionMaxLength = 2_000;

    /// <summary>The earliest founded year accepted; the latest is the tenant's current year.</summary>
    public const int MinFoundedYear = 1800;
}
