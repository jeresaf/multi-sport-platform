using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Contracts.Tenants;

/// <summary>Finds tenants by their identifiers. Reads the global tenant table, so it works before a tenant is set.</summary>
public interface ITenantDirectory
{
    /// <summary>The tenant, or <c>TENANT_NOT_FOUND</c>.</summary>
    Task<Result<TenantSummary>> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>The tenants with these IDs, in any order; an ID that names no tenant is left out.</summary>
    Task<IReadOnlyList<TenantSummary>> GetManyAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken);

    /// <summary>The tenant, or <c>TENANT_NOT_FOUND</c>.</summary>
    Task<Result<TenantSummary>> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant of an <c>ACTIVE</c> domain, for public requests, or <c>TENANT_NOT_FOUND</c> (also for a malformed
    /// host). <paramref name="host"/> may carry a port and any case. Cached for up to a minute.
    /// </summary>
    Task<Result<TenantSummary>> GetByHostAsync(string host, CancellationToken cancellationToken);
}

/// <summary>
/// A tenant's identity, status and the name people know it by (invitation emails, the tenant switcher); no other
/// profile data. Whether its public site is enabled is a tenant-owned setting: <c>ITenantAccess.CheckPublicAccessAsync</c>
/// reads it under the tenant.
/// </summary>
public sealed record TenantSummary(Guid Id, string Slug, TenantStatus Status, string DisplayName);
