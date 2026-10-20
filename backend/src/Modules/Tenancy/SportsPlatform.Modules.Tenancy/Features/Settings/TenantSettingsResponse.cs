using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Domain.Settings;

namespace SportsPlatform.Modules.Tenancy.Features.Settings;

/// <summary>
/// A tenant's settings as the admin API shows them. <c>version</c> (also the <c>ETag</c>) and <c>updatedAt</c> are the
/// settings row's, not the tenant's: send <c>version</c> back with a change to make it fail with
/// <c>409 TENANT_CONCURRENCY_CONFLICT</c> if someone else changed the settings first.
/// </summary>
public sealed record TenantSettingsResponse(
    string TimeZone,
    string DefaultLocale,
    string CurrencyCode,
    bool PublicEnabled,
    int StartingSoonMinutes,
    MinorRegistrationConsent MinorRegistrationConsent,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    /// <summary>Tracked settings, after their save when there was one (the save reads the version back).</summary>
    public static TenantSettingsResponse From(EntityEntry<TenantSettings> entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var s = entry.Entity;
        return new TenantSettingsResponse(
            s.TimeZone, s.DefaultLocale, s.CurrencyCode, s.PublicEnabled, s.StartingSoonMinutes, s.MinorRegistrationConsent,
            entry.Property<DateTimeOffset>(AuditColumns.UpdatedAt).CurrentValue,
            entry.Property<uint>(AuditColumns.Version).CurrentValue);
    }

    /// <summary>The <c>ETag</c>: the quoted version, as for a tenant.</summary>
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}
