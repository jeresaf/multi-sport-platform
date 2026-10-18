using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Host.Tenancy;

/// <summary>Nobody signs in before S03, so nobody is a member of a tenant; Identity replaces this.</summary>
internal sealed class NoTenantMemberships : ITenantMembershipChecker
{
    public ValueTask<bool> IsMemberAsync(Guid tenantId, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
