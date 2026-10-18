using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;

/// <summary>
/// The profile fields to change; a missing or null field stays as it is. The slug and the status can't be changed here
/// (unknown members are rejected). <c>version</c>, when sent, must be the tenant's current version.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTenantProfileRequest(
    string? LegalName,
    string? DisplayName,
    string? CountryCode,
    string? TimeZone,
    string? DefaultLocale,
    string? CurrencyCode,
    bool? PublicEnabled,
    uint? Version);
