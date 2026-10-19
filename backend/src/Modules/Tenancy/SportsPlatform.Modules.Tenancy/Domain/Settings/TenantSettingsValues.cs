namespace SportsPlatform.Modules.Tenancy.Domain.Settings;

/// <summary>
/// A tenant's settings, the allow-listed keys of <c>tenancy.tenant_settings</c>. Codes are checked against Platform's
/// reference data before they get here: <see cref="TimeZone"/> IANA, <see cref="DefaultLocale"/> <c>ll</c> or
/// <c>ll-CC</c>, <see cref="CurrencyCode"/> ISO 4217. <see cref="PublicEnabled"/>: whether the tenant wants a public
/// site; it is shown only while the tenant is <c>ACTIVE</c>. <see cref="StartingSoonMinutes"/>: how long before its
/// start a contest counts as "starting soon" (an edition's ruleset may override it).
/// <see cref="MinorRegistrationConsent"/>: whether registering a minor needs a guardian's consent (Q11).
/// </summary>
public sealed record TenantSettingsValues(
    string TimeZone,
    string DefaultLocale,
    string CurrencyCode,
    bool PublicEnabled,
    int StartingSoonMinutes,
    MinorRegistrationConsent MinorRegistrationConsent)
{
    public const int StartingSoonMinutesMin = 5;
    public const int StartingSoonMinutesMax = 180;
    public const int StartingSoonMinutesDefault = 30;
    public const MinorRegistrationConsent MinorRegistrationConsentDefault = MinorRegistrationConsent.Recommended;

    /// <summary>A new tenant's settings: the given values and the defaults for the rest.</summary>
    public static TenantSettingsValues Defaults(string timeZone, string defaultLocale, string currencyCode, bool publicEnabled) =>
        new(timeZone, defaultLocale, currencyCode, publicEnabled, StartingSoonMinutesDefault, MinorRegistrationConsentDefault);
}
