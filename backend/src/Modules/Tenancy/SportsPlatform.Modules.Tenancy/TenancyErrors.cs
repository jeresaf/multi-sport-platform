using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>The Tenancy module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class TenancyErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "TENANT_CONCURRENCY_CONFLICT", "The tenant was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error NotFound = new(
        "TENANT_NOT_FOUND", "No such tenant.", ErrorKind.NotFound);

    public static readonly Error SlugTaken = new(
        "TENANT_SLUG_TAKEN", "This slug belongs to another tenant, possibly an archived one. Slugs are never reused.", ErrorKind.Conflict);

    /// <summary>Access mode BLOCKED (suspended or archived): admin requests are refused (S02 D9).</summary>
    public static readonly Error Blocked = new(
        "TENANT_BLOCKED", "This tenant is suspended or archived.", ErrorKind.Forbidden);

    /// <summary>
    /// Access mode READ_ONLY (an <c>ACTIVE</c> tenant whose subscription has ended, was cancelled or hasn't started): a
    /// tenant action that isn't available when read-only is refused (S04 D8, D11).
    /// </summary>
    public static readonly Error ReadOnly = new(
        "TENANT_READ_ONLY", "This tenant is read-only: its subscription has ended or hasn't started.", ErrorKind.Forbidden);

    /// <summary>
    /// A tenant action on a <c>PROVISIONING</c> tenant by a member who doesn't hold <c>TENANT_OWNER</c> or
    /// <c>TENANT_ADMIN</c> (S03 D11).
    /// </summary>
    public static readonly Error Provisioning = new(
        "TENANT_PROVISIONING", "This tenant is still being set up; only its owners and administrators can work in it.", ErrorKind.Forbidden);

    /// <summary>A public request for a tenant whose site is hidden or off; says nothing more (S02-T3).</summary>
    public static readonly Error Unavailable = new(
        "TENANT_UNAVAILABLE", "This site is not available.", ErrorKind.Forbidden);

    public static readonly Error InvalidTransition = new(
        "TENANT_INVALID_TRANSITION", "The tenant's status does not allow this change.", ErrorKind.Conflict);

    /// <summary>A plan code in a request that names no active plan (<c>GET /api/platform/v1/plans</c>).</summary>
    public static readonly Error PlanNotFound = new(
        "TENANT_PLAN_NOT_FOUND", "No active plan has this code.", ErrorKind.Validation);

    /// <summary>A new period shares a day with a period that isn't cancelled (S04 D3).</summary>
    public static readonly Error SubscriptionOverlap = new(
        "TENANT_SUBSCRIPTION_OVERLAP", "The period overlaps another subscription period of the tenant.", ErrorKind.Conflict);

    /// <summary>
    /// Renew without any period; change plan without a period in effect today (ACTIVE or EXPIRING); cancel with no
    /// period that is current and not expired, or scheduled (S04 D4).
    /// </summary>
    public static readonly Error SubscriptionNotFound = new(
        "TENANT_SUBSCRIPTION_NOT_FOUND", "The tenant has no subscription period this change applies to.", ErrorKind.Conflict);

    /// <summary>An immediate plan change goes to a higher-ranked plan only; a lower one waits for the renewal (S04 D4).</summary>
    public static readonly Error SubscriptionDowngradeAtRenewal = new(
        "TENANT_SUBSCRIPTION_DOWNGRADE_AT_RENEWAL",
        "The plan can only change now to a higher plan. Choose a lower plan when renewing.",
        ErrorKind.Conflict);

    /// <summary>
    /// An override that names no declared entitlement, or whose value isn't of the entitlement's type or one of its levels
    /// (S04 D5, D11); on the field.
    /// </summary>
    public static readonly Error OverrideInvalidValue = new(
        "TENANT_OVERRIDE_INVALID_VALUE",
        "The override must name a declared entitlement and give a value of its type.",
        ErrorKind.Validation);

    /// <summary>No override of the tenant has this ID, or it has already been removed (S04 D5).</summary>
    public static readonly Error OverrideNotFound = new(
        "TENANT_OVERRIDE_NOT_FOUND", "The tenant has no such entitlement override in place.", ErrorKind.NotFound);

    /// <summary>
    /// Step 2 of the decision formula: the action names a boolean or level entitlement the tenant's plan and overrides
    /// don't give it (S04 D10, D11). <c>detail</c> names the entitlement.
    /// </summary>
    public static readonly Error EntitlementRequired = new(
        "ENTITLEMENT_REQUIRED", "The tenant's plan doesn't include this feature.", ErrorKind.Forbidden);

    /// <summary>
    /// A create that would take the tenant past a quota of its plan or override (S04 D11, D12): retrying never helps, so
    /// it is 403, not 429. Shared with the counting modules through Tenancy's contracts.
    /// </summary>
    public static readonly Error QuotaExceeded = EntitlementFailures.QuotaExceeded;
}
