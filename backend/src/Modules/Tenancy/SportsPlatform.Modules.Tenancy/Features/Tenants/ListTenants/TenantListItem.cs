using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

/// <summary>
/// A tenant in the platform list. No settings: the list runs under no tenant, and settings are tenant-owned (read one
/// tenant for them). <c>version</c> is the tenant row's, as in the detail.
/// </summary>
public sealed record TenantListItem(
    Guid Id,
    string Slug,
    string LegalName,
    string DisplayName,
    string CountryCode,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version)
{
    /// <summary>The query projection (reads the audit columns and <c>xmin</c>).</summary>
    public static Expression<Func<Tenant, TenantListItem>> Projection { get; } = t => new TenantListItem(
        t.Id, t.Slug, t.LegalName, t.DisplayName, t.CountryCode, t.Status,
        EF.Property<DateTimeOffset>(t, AuditColumns.CreatedAt),
        EF.Property<DateTimeOffset>(t, AuditColumns.UpdatedAt),
        EF.Property<uint>(t, AuditColumns.Version));
}
