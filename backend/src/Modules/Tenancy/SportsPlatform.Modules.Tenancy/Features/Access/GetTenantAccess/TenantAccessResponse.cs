using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;

/// <summary>
/// A tenant's access as its owner sees it: <c>status</c>; the <c>accessMode</c> derived from it and the current
/// subscription period; the <c>reason</c> (null when the change needs none, such as an activation) and time
/// (<c>since</c>) of the change that put the tenant in its status; the tenant's <c>timeZone</c> (IANA), in which to show
/// <c>since</c> and the period's days; and the current period's status and days (<c>subscription</c>, null without a
/// period). A read-only tenant's <c>subscription</c> says why; the cancel reason is the platform's and is not shown.
/// </summary>
public sealed record TenantAccessResponse(
    TenantStatus Status,
    TenantAccessMode AccessMode,
    string? Reason,
    DateTimeOffset Since,
    string TimeZone,
    TenantAccessPeriod? Subscription);

/// <summary>
/// The current subscription period on <c>/tenant/access</c>: its status today and its days (all included), in the
/// tenant's time zone. <c>graceEndsOn</c> is the last day of grace; equal to <c>endsOn</c> when the period has no grace
/// days. A response of its own, so a field added to Tenancy's contract for other modules doesn't change the API (#66 R4).
/// </summary>
public sealed record TenantAccessPeriod(SubscriptionStatus Status, DateOnly StartsOn, DateOnly EndsOn, DateOnly GraceEndsOn)
{
    public static TenantAccessPeriod? From(SubscriptionAccess? subscription) => subscription is null
        ? null
        : new(subscription.Status, subscription.StartsOn, subscription.EndsOn, subscription.GraceEndsOn);
}
