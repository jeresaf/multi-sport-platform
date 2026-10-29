using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions;

/// <summary>
/// A tenant's subscription as the platform sees it (S04 D16): every period, latest start first, with its status on
/// <c>today</c>, the tenant's date in <c>timeZone</c>. <c>currentPeriodId</c> is the period that decides access (none
/// without periods); <c>nextPeriodId</c> the next scheduled one. <c>overrides</c> are the tenant's entitlement overrides
/// with their reasons and history, unremoved first (S04 D5): platform-only. <c>entitlements</c> are every declared
/// entitlement's effective value today, by code, with its source (D6). Usage arrives with S04-T3b.
/// </summary>
public sealed record SubscriptionResponse(
    Guid TenantId,
    DateOnly Today,
    string TimeZone,
    Guid? CurrentPeriodId,
    Guid? NextPeriodId,
    IReadOnlyList<SubscriptionPeriodResponse> Periods,
    IReadOnlyList<EntitlementOverrideResponse> Overrides,
    IReadOnlyList<EffectiveEntitlementResponse> Entitlements)
{
    /// <summary>The periods and overrides must be tracked by <paramref name="context"/> (their audit columns are read from it).</summary>
    internal static SubscriptionResponse From(
        TenancyDbContext context,
        Guid tenantId,
        TenantSubscription subscription,
        TenantEntitlementOverrides overrides,
        DateOnly today,
        string timeZone,
        IReadOnlyList<EntitlementDefinition> definitions,
        PlanValues planValues)
    {
        var current = subscription.CurrentOn(today);
        var effective = new EffectiveEntitlements(current?.PlanCode, planValues.Of(current?.PlanCode), overrides.InEffectOn(today));
        return new(
            tenantId,
            today,
            timeZone,
            current?.Id,
            subscription.NextScheduledOn(today)?.Id,
            [.. subscription.Periods.Select(period => SubscriptionPeriodResponse.From(context, period, today))],
            [.. overrides.Overrides.Select(entitlementOverride => EntitlementOverrideResponse.From(context, entitlementOverride, today))],
            [.. definitions.Select(definition => EffectiveEntitlementResponse.From(effective.Of(definition)))]);
    }
}

/// <summary>
/// A tenant's value of one entitlement today and where it comes from: <c>DEFAULT</c>, <c>PLAN</c> (the current period's
/// plan) or <c>OVERRIDE</c> (an unremoved override in effect).
/// </summary>
public sealed record EffectiveEntitlementResponse(string Code, EntitlementValue Value, EntitlementSource Source)
{
    internal static EffectiveEntitlementResponse From(EffectiveEntitlement entitlement) =>
        new(entitlement.Code, entitlement.Value, entitlement.Source);
}

/// <summary>
/// One period: <c>startsOn</c> to <c>endsOn</c>, both days included. <c>expiringFrom</c> is the first day of
/// <c>EXPIRING</c> (after <c>endsOn</c> when the window is 0), <c>graceEndsOn</c> the last day of <c>GRACE</c>. The
/// cancellation fields are set together.
/// </summary>
public sealed record SubscriptionPeriodResponse(
    Guid Id,
    string PlanCode,
    SubscriptionKind Kind,
    SubscriptionStatus Status,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int ExpiringWindowDays,
    int GraceDays,
    DateOnly ExpiringFrom,
    DateOnly GraceEndsOn,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset? CancelledAt,
    Guid? CancelledBy,
    string? CancelReason)
{
    internal static SubscriptionPeriodResponse From(TenancyDbContext context, SubscriptionPeriod period, DateOnly today)
    {
        var entry = context.Entry(period);
        return new SubscriptionPeriodResponse(
            period.Id,
            period.PlanCode,
            period.Kind,
            period.StatusOn(today),
            period.StartsOn,
            period.EndsOn,
            period.ExpiringWindowDays,
            period.GraceDays,
            period.ExpiringFrom,
            period.GraceEndsOn,
            entry.Property<DateTimeOffset>(AuditColumns.CreatedAt).CurrentValue,
            entry.Property<Guid>(AuditColumns.CreatedBy).CurrentValue,
            period.CancelledAt,
            period.CancelledBy,
            period.CancelReason);
    }
}

/// <summary>
/// One entitlement override: <c>value</c> in place of the plan's for <c>entitlementCode</c>, until <c>effectiveUntil</c>
/// (included; none = until removed). <c>inEffect</c>: it applies on the response's <c>today</c> (not removed, not past
/// its last day). The removal fields are set together.
/// </summary>
public sealed record EntitlementOverrideResponse(
    Guid Id,
    string EntitlementCode,
    EntitlementValue Value,
    string Reason,
    DateOnly? EffectiveUntil,
    bool InEffect,
    DateTimeOffset GrantedAt,
    Guid GrantedBy,
    DateTimeOffset? RemovedAt,
    Guid? RemovedBy,
    string? RemoveReason)
{
    internal static EntitlementOverrideResponse From(TenancyDbContext context, EntitlementOverride entitlementOverride, DateOnly today)
    {
        var entry = context.Entry(entitlementOverride);
        return new EntitlementOverrideResponse(
            entitlementOverride.Id,
            entitlementOverride.EntitlementCode,
            entitlementOverride.Value,
            entitlementOverride.Reason,
            entitlementOverride.EffectiveUntil,
            entitlementOverride.IsInEffectOn(today),
            entry.Property<DateTimeOffset>(AuditColumns.CreatedAt).CurrentValue,
            entry.Property<Guid>(AuditColumns.CreatedBy).CurrentValue,
            entitlementOverride.RemovedAt,
            entitlementOverride.RemovedBy,
            entitlementOverride.RemoveReason);
    }
}
