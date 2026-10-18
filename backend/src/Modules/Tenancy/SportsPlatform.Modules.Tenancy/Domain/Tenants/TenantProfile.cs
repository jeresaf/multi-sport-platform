namespace SportsPlatform.Modules.Tenancy.Domain.Tenants;

/// <summary>
/// The fields of a tenant that the platform edits freely. Codes are checked against Platform's reference data before
/// they get here: <see cref="CountryCode"/> ISO 3166-1 alpha-2, <see cref="TimeZone"/> IANA, <see cref="DefaultLocale"/>
/// <c>ll</c> or <c>ll-CC</c> (ISO 639-1 language, optional country), <see cref="CurrencyCode"/> ISO 4217.
/// </summary>
public sealed record TenantProfile(
    string LegalName,
    string DisplayName,
    string CountryCode,
    string TimeZone,
    string DefaultLocale,
    string CurrencyCode,
    bool PublicEnabled)
{
    public const int LegalNameMaxLength = 200;
    public const int DisplayNameMaxLength = 100;
}
