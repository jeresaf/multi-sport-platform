namespace SportsPlatform.Modules.Platform.Contracts.Reference;

/// <summary>
/// Checks codes against Platform's reference data (countries, time zones, currencies, languages) so other modules
/// validate them without reading Platform's tables. Codes match exactly, in their canonical case: <c>UG</c>,
/// <c>Africa/Kampala</c>, <c>UGX</c>, <c>en</c>. Only active entries count.
/// </summary>
public interface IReferenceData
{
    /// <summary>An active ISO 3166-1 alpha-2 country code.</summary>
    Task<bool> IsActiveCountryAsync(string code, CancellationToken cancellationToken);

    /// <summary>An active IANA time zone ID.</summary>
    Task<bool> IsActiveTimeZoneAsync(string code, CancellationToken cancellationToken);

    /// <summary>An active ISO 4217 currency code (money only).</summary>
    Task<bool> IsActiveCurrencyAsync(string code, CancellationToken cancellationToken);

    /// <summary>An active ISO 639-1 language code.</summary>
    Task<bool> IsActiveLanguageAsync(string code, CancellationToken cancellationToken);
}
