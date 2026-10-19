using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Contracts.Tenants;

/// <summary>Finds tenants by their identifiers. Reads the global tenant table, so it works before a tenant is set.</summary>
public interface ITenantDirectory
{
    /// <summary>The tenant, or <c>TENANT_NOT_FOUND</c>.</summary>
    Task<Result<TenantSummary>> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TenantSummary?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant of an <c>ACTIVE</c> domain, for public requests. <paramref name="host"/> may carry a port and any case;
    /// null for an unknown or malformed host. Cached for up to a minute.
    /// </summary>
    Task<TenantSummary?> FindByHostAsync(string host, CancellationToken cancellationToken);
}

/// <summary>A tenant's identity and visibility; no profile data.</summary>
public sealed record TenantSummary(Guid Id, string Slug, TenantStatus Status, bool PublicEnabled);
