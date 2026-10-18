using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// A tenant as the platform API shows it. <see cref="Version"/> is the row version: send it back with a change to
/// make it fail with <c>409 TENANT_CONCURRENCY_CONFLICT</c> if someone else changed the tenant first.
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
    /// <summary>The query projection (reads the audit columns and <c>xmin</c>).</summary>
    public static Expression<Func<Tenant, TenantResponse>> Projection { get; } = t => new TenantResponse(
        t.Id, t.Slug, t.LegalName, t.DisplayName, t.CountryCode, t.TimeZone, t.DefaultLocale, t.CurrencyCode, t.Status,
        t.PublicEnabled,
        EF.Property<DateTimeOffset>(t, AuditColumns.CreatedAt),
        EF.Property<DateTimeOffset>(t, AuditColumns.UpdatedAt),
        EF.Property<uint>(t, AuditColumns.Version));

    /// <summary>A tracked tenant after its save (the version is read back by the save).</summary>
    public static TenantResponse From(EntityEntry<Tenant> entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var t = entry.Entity;
        return new TenantResponse(
            t.Id, t.Slug, t.LegalName, t.DisplayName, t.CountryCode, t.TimeZone, t.DefaultLocale, t.CurrencyCode, t.Status,
            t.PublicEnabled,
            entry.Property<DateTimeOffset>(AuditColumns.CreatedAt).CurrentValue,
            entry.Property<DateTimeOffset>(AuditColumns.UpdatedAt).CurrentValue,
            entry.Property<uint>(AuditColumns.Version).CurrentValue);
    }

    /// <summary>The <c>ETag</c> of a single tenant: its quoted version.</summary>
    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = $"\"{Version}\"";
    }
}
