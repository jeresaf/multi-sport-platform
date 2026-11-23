namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// A team's editable fields (S07 D6, D7), trimmed and checked by the use case: the request's shape by its validator, the
/// organisation and home venue against the tenant's, the colours' order, the founded year against the tenant's year. A
/// value out of bounds here is a bug. Blank optional texts are null; colours are upper case. The name is here for a
/// create only: an update keeps it (S07 D8, rename).
/// </summary>
public sealed record TeamDetails(
    string Name,
    string? ShortName,
    TeamCategory Category,
    Guid? OrganisationId,
    string? Code,
    string? Motto,
    string? PrimaryColour,
    string? SecondaryColour,
    string? TertiaryColour,
    Guid? HomeVenueId,
    int? FoundedYear)
{
    public const int NameMaxLength = 120;
    public const int ShortNameMaxLength = 30;
    public const int MottoMaxLength = 120;
    public const int CodeLength = 3;
    public const int ColourLength = 7;
    public const int VariantMaxLength = 50;

    /// <summary>The earliest founded year accepted; the latest is the tenant's current year.</summary>
    public const int MinFoundedYear = 1800;

    /// <summary>The earliest date a name may start from (<c>name_since</c>), as the founded year's floor.</summary>
    public static readonly DateOnly MinNameSince = new(MinFoundedYear, 1, 1);

    /// <summary>
    /// Whether the colours come in order (S07 D6): a secondary needs a primary, a tertiary a secondary. Otherwise
    /// <c>TEAM_COLOURS_INVALID</c>.
    /// </summary>
    public static bool ColoursInOrder(string? primary, string? secondary, string? tertiary) =>
        (secondary is null || primary is not null) && (tertiary is null || secondary is not null);
}
