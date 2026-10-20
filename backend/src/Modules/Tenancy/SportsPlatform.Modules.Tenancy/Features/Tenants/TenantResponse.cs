using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// A tenant as the platform API shows it: the tenant and its main settings. Read under the tenant (the settings are
/// tenant-owned). <c>version</c> (also the <c>ETag</c>), <c>createdAt</c> and <c>updatedAt</c> are the tenant row's
/// only: a settings change doesn't change them, so the same <c>ETag</c> can come with different settings. Send
/// <c>version</c> back with a change to make it fail with <c>409 TENANT_CONCURRENCY_CONFLICT</c> if someone else changed
/// the tenant first. The settings have their own version at <c>/api/admin/v1/tenant/settings</c>.
/// </summary>
public sealed record TenantResponse(
    Guid Id,
    string Slug,
    string LegalName,
    string DisplayName,
    string CountryCode,
    string TimeZone,
    string DefaultLocale,
    string CurrencyCode,
    TenantStatus Status,
    bool PublicEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    // Reads the tenant's audit columns and xmin.
    private static readonly Expression<Func<Tenant, TenantSettings, TenantResponse>> _projection = (t, s) => new TenantResponse(
        t.Id, t.Slug, t.LegalName, t.DisplayName, t.CountryCode, s.TimeZone, s.DefaultLocale, s.CurrencyCode, t.Status,
        s.PublicEnabled,
        EF.Property<DateTimeOffset>(t, AuditColumns.CreatedAt),
        EF.Property<DateTimeOffset>(t, AuditColumns.UpdatedAt),
        EF.Property<uint>(t, AuditColumns.Version));

    /// <summary>One tenant with its settings; the caller runs under that tenant, or the settings aren't visible.</summary>
    public static Task<TenantResponse?> FindAsync(TenancyDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Join(context.Set<TenantSettings>().AsNoTracking(), t => t.Id, s => s.TenantId, _projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>A tracked tenant after its save (the version is read back by the save), with its settings.</summary>
    public static TenantResponse From(EntityEntry<Tenant> entry, TenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(settings);
        var t = entry.Entity;
        return new TenantResponse(
            t.Id, t.Slug, t.LegalName, t.DisplayName, t.CountryCode, settings.TimeZone, settings.DefaultLocale,
            settings.CurrencyCode, t.Status, settings.PublicEnabled,
            entry.Property<DateTimeOffset>(AuditColumns.CreatedAt).CurrentValue,
            entry.Property<DateTimeOffset>(AuditColumns.UpdatedAt).CurrentValue,
            entry.Property<uint>(AuditColumns.Version).CurrentValue);
    }

    /// <summary>The <c>ETag</c> of a single tenant: its quoted version.</summary>
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}
