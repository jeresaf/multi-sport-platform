using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>
/// A tenant's access today (state-machines.md, "Access mode"): its status, the access mode derived from it and the
/// current subscription period, and the tenant's time zone (an IANA ID), in which the period's days begin and end.
/// <c>Subscription</c> is null when the tenant has no period. For handlers that check their own window while
/// read-only (S11, S13) and for the tenant's access read.
/// </summary>
public sealed record TenantAccess(TenantStatus Status, TenantAccessMode Mode, string TimeZone, SubscriptionAccess? Subscription)
{
    /// <summary>
    /// Whether the access mode lets <paramref name="action"/> (a tenant action) through: every action when <c>FULL</c>,
    /// those available when read-only when <c>READ_ONLY</c>, those available when blocked when <c>BLOCKED</c>. Step 1's
    /// rule, shared with the capabilities list (S04 D14). A <c>PROVISIONING</c> tenant is <c>FULL</c> here: step 1 also
    /// checks that the actor may set it up, which this record can't tell.
    /// </summary>
    public bool Admits(ActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Mode switch
        {
            TenantAccessMode.Full => true,
            TenantAccessMode.ReadOnly => action.AvailableWhenReadOnly,
            TenantAccessMode.Blocked => action.AvailableWhenBlocked,
            _ => throw new ArgumentOutOfRangeException(nameof(action), Mode, "Unknown access mode."),
        };
    }
}

/// <summary>
/// The current subscription period as access sees it: its status today and its days (all included), in the tenant's
/// time zone. <c>graceEndsOn</c> is the last day of grace; equal to <c>endsOn</c> when the period has no grace days.
/// </summary>
public sealed record SubscriptionAccess(SubscriptionStatus Status, DateOnly StartsOn, DateOnly EndsOn, DateOnly GraceEndsOn);
