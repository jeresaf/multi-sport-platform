using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.GetTenantSubscription;

/// <summary>
/// A tenant's subscription as its members see it (S04 D15): <c>today</c> in the tenant's <c>timeZone</c>; the
/// <c>current</c> period (the one that decides access; null without periods) and the <c>next</c> scheduled one; every
/// declared entitlement's effective value with its source; and every quota's usage. No override reasons, cancel
/// reasons or who made a change: those are the platform's.
/// </summary>
public sealed record TenantSubscriptionResponse(
    DateOnly Today,
    string TimeZone,
    TenantSubscriptionPeriod? Current,
    TenantSubscriptionPeriod? Next,
    IReadOnlyList<TenantEntitlementResponse> Entitlements,
    IReadOnlyList<QuotaUsageResponse> Usage);

/// <summary>
/// A period's plan (code and name), kind and status today, and its days, all included: <c>expiringFrom</c> is the first
/// day of <c>EXPIRING</c> (after <c>endsOn</c> when the window is 0), <c>graceEndsOn</c> the last day of <c>GRACE</c>.
/// </summary>
public sealed record TenantSubscriptionPeriod(
    string PlanCode,
    string PlanName,
    SubscriptionKind Kind,
    SubscriptionStatus Status,
    DateOnly StartsOn,
    DateOnly EndsOn,
    DateOnly ExpiringFrom,
    DateOnly GraceEndsOn)
{
    internal static TenantSubscriptionPeriod? From(SubscriptionPeriod? period, string? planName, DateOnly today) => period is null
        ? null
        : new(period.PlanCode, planName ?? period.PlanCode, period.Kind, period.StatusOn(today), period.StartsOn, period.EndsOn,
            period.ExpiringFrom, period.GraceEndsOn);
}

/// <summary>
/// One entitlement as the tenant's Subscription page shows it: its name, description, type, unit (quotas) and levels
/// (level entitlements, lowest first), and the tenant's value today with its source (<c>DEFAULT</c>, <c>PLAN</c> or
/// <c>OVERRIDE</c>).
/// </summary>
public sealed record TenantEntitlementResponse(
    string Code,
    string Name,
    string Description,
    EntitlementType Type,
    string? Unit,
    IReadOnlyList<string> Levels,
    EntitlementValue Value,
    EntitlementSource Source)
{
    internal static TenantEntitlementResponse From(EntitlementDefinition definition, EffectiveEntitlement effective) =>
        new(definition.Code, definition.Name, definition.Description, definition.Type, definition.Unit, definition.Levels,
            effective.Value, effective.Source);
}
