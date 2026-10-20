using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;

/// <summary>
/// A new tenant. Every field but <c>publicEnabled</c> (default false) is required; unknown members are rejected,
/// so a status can't be sent (a new tenant is always <c>PROVISIONING</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateTenantRequest(
    string? Slug,
    string? LegalName,
    string? DisplayName,
    string? CountryCode,
    string? TimeZone,
    string? DefaultLocale,
    string? CurrencyCode,
    bool? PublicEnabled = null);
