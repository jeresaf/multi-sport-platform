namespace SportsPlatform.BuildingBlocks.Tenancy;

/// <summary>
/// Whether the current actor may work in a tenant through the admin API (the <c>X-Tenant-Id</c> header). The Host's
/// default grants no memberships; Identity replaces it in S03.
/// </summary>
public interface ITenantMembershipChecker
{
    ValueTask<bool> IsMemberAsync(Guid tenantId, CancellationToken cancellationToken);
}
