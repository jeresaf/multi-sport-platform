using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;

namespace SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;

/// <summary>
/// The tenant capabilities (S04 D14): <c>actions</c>, the action codes the caller may perform now (all three steps of
/// the decision formula), in ordinal order; <c>unavailable</c>, the actions the caller's roles grant but the tenant
/// can't use now, with the reason, in ordinal order; <c>access</c>, the tenant's access for the banner;
/// <c>scope</c>, the resource asked about, if any. Actions the roles don't grant are in neither list.
/// </summary>
public sealed record TenantCapabilitiesResponse(
    IReadOnlyList<string> Actions,
    IReadOnlyList<UnavailableActionResponse> Unavailable,
    CapabilityAccessResponse Access,
    CapabilityScopeResponse? Scope);

/// <summary>
/// An action the caller's roles grant that the tenant can't use now. <c>code</c> is the refusal the request would get
/// first: <c>TENANT_READ_ONLY</c> (step 1) or <c>ENTITLEMENT_REQUIRED</c> (step 2). For <c>ENTITLEMENT_REQUIRED</c>,
/// <c>entitlement</c> is the entitlement the action names and <c>plans</c> the active plans, in rank order, that include
/// it (empty when none does); both are null for <c>TENANT_READ_ONLY</c>.
/// </summary>
public sealed record UnavailableActionResponse(string Action, string Code, string? Entitlement, IReadOnlyList<string>? Plans);

/// <summary>
/// The tenant's access today, for the shell's banner: the access <c>mode</c>; the current subscription period's status
/// and its last day and last day of grace (all null without a period); the tenant's <c>timeZone</c> (IANA), in which the
/// days begin and end. A response of its own, so a field added to Tenancy's contract doesn't change the API.
/// </summary>
public sealed record CapabilityAccessResponse(
    TenantAccessMode Mode, SubscriptionStatus? SubscriptionStatus, DateOnly? EndsOn, DateOnly? GraceEndsOn, string TimeZone)
{
    public static CapabilityAccessResponse From(TenantAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return new(access.Mode, access.Subscription?.Status, access.Subscription?.EndsOn, access.Subscription?.GraceEndsOn, access.TimeZone);
    }
}
