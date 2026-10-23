using SportsPlatform.Modules.Tenancy.Contracts.Access;
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
    SubscriptionAccess? Subscription);
