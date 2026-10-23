using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions;

/// <summary>
/// A tenant's subscription as the platform sees it (S04 D16): every period, latest start first, with its status on
/// <c>today</c>, the tenant's date in <c>timeZone</c>. <c>currentPeriodId</c> is the period that decides access (none
/// without periods); <c>nextPeriodId</c> the next scheduled one. Overrides, effective entitlements and usage arrive with
/// S04-T3.
/// </summary>
public sealed record SubscriptionResponse(
    Guid TenantId,
    DateOnly Today,
    string TimeZone,
    Guid? CurrentPeriodId,
    Guid? NextPeriodId,
    IReadOnlyList<SubscriptionPeriodResponse> Periods)
{
    /// <summary>The periods must be tracked by <paramref name="context"/> (their audit columns are read from it).</summary>
    internal static SubscriptionResponse From(
        TenancyDbContext context, Guid tenantId, TenantSubscription subscription, DateOnly today, string timeZone) => new(
        tenantId,
        today,
        timeZone,
        subscription.CurrentOn(today)?.Id,
        subscription.NextScheduledOn(today)?.Id,
        [.. subscription.Periods.Select(period => SubscriptionPeriodResponse.From(context, period, today))]);
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
