using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Platform.Domain.Reference;

/// <summary>
/// An IANA time zone; <see cref="ReferenceEntry.Code"/> is its ID (<c>Africa/Kampala</c>). Named so it doesn't clash
/// with <see cref="System.TimeZone"/>.
/// </summary>
public sealed class IanaTimeZone : ReferenceEntry
{
    private string? _countryCode;

    public IanaTimeZone(string code, string? countryCode)
        : base(IdFor(code), code) => Apply(countryCode);

    private IanaTimeZone()
    {
    }

    /// <summary>The ISO 3166-1 alpha-2 code of the zone's country (tzdata <c>zone.tab</c>); null for <c>Etc/UTC</c>.</summary>
    public string? CountryCode { get => _countryCode; private set => _countryCode = value; }

    public static Guid IdFor(string code) => DeterministicIds.Create($"platform.time_zone:{code}");

    /// <summary>Takes the values from the reference data; returns whether anything changed.</summary>
    public bool Apply(string? countryCode) => Set(ref _countryCode, countryCode) | Reactivate();
}
