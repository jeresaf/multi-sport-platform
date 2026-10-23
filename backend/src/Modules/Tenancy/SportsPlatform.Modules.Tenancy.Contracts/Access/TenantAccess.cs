using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Contracts.Access;

/// <summary>
/// A tenant's access today (state-machines.md, "Access mode"): its status, the access mode derived from it and the
/// current subscription period, and the tenant's time zone (an IANA ID), in which the period's days begin and end.
/// <c>Subscription</c> is null when the tenant has no period. For handlers that check their own window while
/// read-only (S11, S13) and for the tenant's access read.
/// </summary>
public sealed record TenantAccess(TenantStatus Status, TenantAccessMode Mode, string TimeZone, SubscriptionAccess? Subscription);

/// <summary>
/// The current subscription period as access sees it: its status today and its days (all included), in the tenant's
/// time zone. <c>graceEndsOn</c> is the last day of grace (<c>endsOn</c> without grace days).
/// </summary>
public sealed record SubscriptionAccess(SubscriptionStatus Status, DateOnly StartsOn, DateOnly EndsOn, DateOnly GraceEndsOn);
