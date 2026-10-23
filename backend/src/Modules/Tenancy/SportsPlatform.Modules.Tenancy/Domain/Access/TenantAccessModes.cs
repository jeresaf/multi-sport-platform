using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Domain.Access;

/// <summary>The access mode from the tenant's status and its current subscription period (state-machines.md, "Access mode").</summary>
public static class TenantAccessModes
{
    /// <summary>
    /// <c>PROVISIONING</c> is <c>FULL</c> (step 1 lets only those who set it up through); <c>ACTIVE</c> is <c>FULL</c>
    /// while the current period is in effect (<c>ACTIVE</c>, <c>EXPIRING</c>, <c>GRACE</c>) and <c>READ_ONLY</c> when it
    /// has expired, was cancelled or hasn't started, or there is none (S04 D8); suspended and archived are
    /// <c>BLOCKED</c>. <paramref name="current"/> is the current period's status today, null without a period.
    /// </summary>
    public static TenantAccessMode Of(TenantStatus status, SubscriptionStatus? current) => status switch
    {
        TenantStatus.Provisioning => TenantAccessMode.Full,
        TenantStatus.Active => current is SubscriptionStatus.Active or SubscriptionStatus.Expiring or SubscriptionStatus.Grace
            ? TenantAccessMode.Full
            : TenantAccessMode.ReadOnly,
        TenantStatus.Suspended or TenantStatus.Archived => TenantAccessMode.Blocked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
