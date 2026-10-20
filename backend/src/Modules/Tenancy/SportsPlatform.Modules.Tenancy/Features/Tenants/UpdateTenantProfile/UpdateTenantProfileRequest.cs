using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;

/// <summary>
/// The profile fields to change; a missing or null field stays as it is. The slug and the status can't be changed here
/// (unknown members are rejected). <c>version</c>, when sent, must be the tenant's current version.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTenantProfileRequest(
    string? LegalName = null,
    string? DisplayName = null,
    string? CountryCode = null,
    string? TimeZone = null,
    string? DefaultLocale = null,
    string? CurrencyCode = null,
    bool? PublicEnabled = null,
    uint? Version = null);
