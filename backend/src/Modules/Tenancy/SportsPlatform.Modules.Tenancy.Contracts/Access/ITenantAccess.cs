using SportsPlatform.BuildingBlocks.Results;

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
}
