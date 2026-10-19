using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>A tenant's access mode, checked first for every admin request (docs/architecture/authorization.md).</summary>
public interface ITenantAccess
{
    /// <summary>The tenant's access mode, or null for an unknown tenant.</summary>
    Task<TenantAccessMode?> GetAccessModeAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Step 1 of the decision formula for an admin request: success for <c>FULL</c>, <c>TENANT_BLOCKED</c> for
    /// <c>BLOCKED</c>, <c>TENANT_NOT_FOUND</c> for an unknown tenant.
    /// </summary>
    Task<Result> CheckAdminAccessAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the tenant's public site is shown: success for an <c>ACTIVE</c> tenant with <c>publicEnabled</c>, else
    /// <c>TENANT_UNAVAILABLE</c> (hidden while <c>PROVISIONING</c> or not enabled, off while <c>BLOCKED</c>).
    /// </summary>
    Result CheckPublicAccess(TenantSummary tenant);
}
