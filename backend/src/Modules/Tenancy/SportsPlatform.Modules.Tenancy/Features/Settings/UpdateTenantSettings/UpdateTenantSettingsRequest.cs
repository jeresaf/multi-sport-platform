using System.Text.Json.Serialization;
using SportsPlatform.Modules.Tenancy.Domain.Settings;

namespace SportsPlatform.Modules.Tenancy.Features.Settings.UpdateTenantSettings;

/// <summary>
/// The settings to change; a missing or null key stays as it is. Only these keys exist: any other key, or a value of
/// the wrong type, is named in <c>errors</c>. <c>version</c>, when sent, must be the settings' current version.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTenantSettingsRequest(
    string? TimeZone = null,
    string? DefaultLocale = null,
    string? CurrencyCode = null,
    bool? PublicEnabled = null,
    int? StartingSoonMinutes = null,
    MinorRegistrationConsent? MinorRegistrationConsent = null,
    uint? Version = null);
