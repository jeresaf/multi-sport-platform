using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>A tenant's access mode, checked first for every admin request (docs/architecture/authorization.md).</summary>
public interface ITenantAccess
{
    /// <summary>
    /// The tenant's access today: status, access mode, time zone and current subscription period; null for an unknown
    /// tenant. The settings and periods are tenant-owned, so call this after the scope's tenant is set to
    /// <paramref name="tenantId"/>.
    /// </summary>
    Task<TenantAccess?> GetAccessAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Step 1 of the decision formula for an admin request's tenant <paramref name="action"/> (S03 D11, S04 D8): success
    /// for an <c>ACTIVE</c> tenant whose current subscription period is in effect (<c>FULL</c>); for one whose period has
    /// expired, was cancelled or hasn't started, or that has none (<c>READ_ONLY</c>), success only for an action
    /// available when read-only, else <c>TENANT_READ_ONLY</c>; for a <c>PROVISIONING</c> one, success when the actor may
    /// set it up (<see cref="BuildingBlocks.Tenancy.ITenantSetupAccess"/>), else <c>TENANT_PROVISIONING</c>; for a
    /// <c>BLOCKED</c> one, success only for an action available when blocked, else <c>TENANT_BLOCKED</c>;
    /// <c>TENANT_NOT_FOUND</c> for an unknown tenant. Call after the scope's tenant is set to <paramref name="tenantId"/>.
    /// </summary>
    Task<Result> CheckAdminAccessAsync(Guid tenantId, ActionDefinition action, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the tenant's public site is shown: success for an <c>ACTIVE</c> tenant whose settings enable it, else
    /// <c>TENANT_UNAVAILABLE</c> (hidden while <c>PROVISIONING</c> or not enabled, off while <c>BLOCKED</c>). The setting
    /// is tenant-owned, so call this after setting the scope's tenant to <paramref name="tenant"/>. Cached for up to a
    /// minute.
    /// </summary>
    Task<Result> CheckPublicAccessAsync(TenantSummary tenant, CancellationToken cancellationToken);
}
