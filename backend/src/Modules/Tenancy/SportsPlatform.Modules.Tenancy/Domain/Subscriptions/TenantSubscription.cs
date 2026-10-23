using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

/// <summary>
/// A tenant's subscription periods and the rules between them (S04 D3, D4): non-cancelled periods never overlap, the
/// current period, and the four changes. Load every period of the tenant under the tenant's subscription lock before
/// a change, so two changes can't both pass the overlap check. "Today" is the tenant's date (<see cref="TenantCalendar"/>).
/// </summary>
public sealed class TenantSubscription
{
    private readonly List<SubscriptionPeriod> _periods;

    public TenantSubscription(IEnumerable<SubscriptionPeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        _periods = [.. periods];
    }

    /// <summary>Latest start first; periods starting on the same day, the one created last first (UUIDv7).</summary>
    public IReadOnlyList<SubscriptionPeriod> Periods =>
        [.. _periods.OrderByDescending(p => p.StartsOn).ThenByDescending(p => p.Id)];

    /// <summary>The period with the latest start, of any kind.</summary>
    private SubscriptionPeriod? Latest => Periods is [var latest, ..] ? latest : null;

    /// <summary>
    /// The period that decides access today (S04 D3, as the owner refined it in #65 R2): the latest-started period in
    /// effect today (ACTIVE, EXPIRING or GRACE); with none, the latest-started period of any kind, so an expired or
    /// cancelled one says why; with none started, the latest period (scheduled); with no periods, none. A period
    /// cancelled by mistake never hides a valid one still in effect.
    /// </summary>
    public SubscriptionPeriod? CurrentOn(DateOnly today)
    {
        var started = Periods.Where(p => p.StartsOn <= today).ToList();
        return started.Find(p => IsInEffect(p, today)) ?? (started.Count > 0 ? started[0] : Latest);
    }

    /// <summary>The next non-cancelled period that starts after today, if any.</summary>
    public SubscriptionPeriod? NextScheduledOn(DateOnly today) =>
        _periods.Where(p => !p.IsCancelled && p.StartsOn > today).OrderBy(p => p.StartsOn).FirstOrDefault();

    /// <summary><c>subscription.assign</c>: a new period on the dates given, refused if it overlaps another.</summary>
    public Result<SubscriptionPeriod> Assign(SubscriptionTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        return Add(terms);
    }

    /// <summary>
    /// The first day of a renewal: the day after the latest non-cancelled period ends, or today if that day has
    /// passed or every period is cancelled. Null without any period (nothing to renew).
    /// </summary>
    public DateOnly? RenewalStartOn(DateOnly today)
    {
        if (_periods.Count == 0)
        {
            return null;
        }

        var latestEnd = _periods.Where(p => !p.IsCancelled).Select(p => (DateOnly?)p.EndsOn).Max();
        return latestEnd is { } end && end.AddDays(1) > today ? end.AddDays(1) : today;
    }

    /// <summary>
    /// <c>subscription.renew</c>: a new period from <see cref="RenewalStartOn"/> to <paramref name="endsOn"/>. The
    /// plan, kind and windows not given are the latest period's, so a downgrade happens here (pricing-and-plans.md).
    /// <see cref="CommonErrors.InvalidRequest"/> when <paramref name="endsOn"/> is before the first day.
    /// </summary>
    public Result<SubscriptionPeriod> Renew(
        DateOnly endsOn, string? planCode, SubscriptionKind? kind, int? expiringWindowDays, int? graceDays, DateOnly today)
    {
        if (RenewalStartOn(today) is not { } startsOn || Latest is not { } latest)
        {
            return TenancyErrors.SubscriptionNotFound;
        }

        if (endsOn < startsOn)
        {
            return CommonErrors.InvalidRequest;
        }

        return Add(new SubscriptionTerms(
            planCode ?? latest.PlanCode,
            kind ?? latest.Kind,
            startsOn,
            endsOn,
            expiringWindowDays ?? latest.ExpiringWindowDays,
            graceDays ?? latest.GraceDays));
    }

    /// <summary>
    /// <c>subscription.change_plan</c>: now, to a higher-ranked plan only. Cancels the current period ("Changed to
    /// &lt;plan&gt;") and adds one on <paramref name="planCode"/> from today to the same last day, with the same kind and
    /// windows. The current period must be in effect today (ACTIVE or EXPIRING); after it ends, renew instead.
    /// <paramref name="rankOf"/> gives a plan's rank, active or not.
    /// </summary>
    public Result<SubscriptionPeriod> ChangePlan(
        string planCode, Func<string, int?> rankOf, ICurrentActor actor, DateTimeOffset now, DateOnly today)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planCode);
        ArgumentNullException.ThrowIfNull(rankOf);
        if (CurrentOn(today) is not { } current
            || current.StatusOn(today) is not (SubscriptionStatus.Active or SubscriptionStatus.Expiring))
        {
            return TenancyErrors.SubscriptionNotFound;
        }

        if (rankOf(planCode) is not { } target || rankOf(current.PlanCode) is { } rank && target <= rank)
        {
            return TenancyErrors.SubscriptionDowngradeAtRenewal;
        }

        current.Cancel($"Changed to {planCode}", actor, now);
        return Add(new SubscriptionTerms(
            planCode, current.Kind, today, current.EndsOn, current.ExpiringWindowDays, current.GraceDays));
    }

    /// <summary>
    /// <c>subscription.cancel</c>: cancels, now, every period that isn't cancelled or expired: those in effect today
    /// (an earlier one still in GRACE included, #65 R1) and every scheduled one; returns them.
    /// </summary>
    public Result<IReadOnlyList<SubscriptionPeriod>> Cancel(string reason, ICurrentActor actor, DateTimeOffset now, DateOnly today)
    {
        var cancelled = _periods
            .Where(p => p.StatusOn(today) is not (SubscriptionStatus.Cancelled or SubscriptionStatus.Expired))
            .OrderBy(p => p.StartsOn)
            .ToList();
        if (cancelled.Count == 0)
        {
            return TenancyErrors.SubscriptionNotFound;
        }

        foreach (var period in cancelled)
        {
            period.Cancel(reason, actor, now);
        }

        return Result.Success<IReadOnlyList<SubscriptionPeriod>>(cancelled);
    }

    private static bool IsInEffect(SubscriptionPeriod period, DateOnly today) =>
        period.StatusOn(today) is SubscriptionStatus.Active or SubscriptionStatus.Expiring or SubscriptionStatus.Grace;

    private Result<SubscriptionPeriod> Add(SubscriptionTerms terms)
    {
        if (_periods.Any(p => p.Overlaps(terms.StartsOn, terms.EndsOn)))
        {
            return TenancyErrors.SubscriptionOverlap;
        }

        var period = SubscriptionPeriod.Create(
            terms.PlanCode, terms.Kind, terms.StartsOn, terms.EndsOn, terms.ExpiringWindowDays, terms.GraceDays);
        _periods.Add(period);
        return Result.Success(period);
    }
}

/// <summary>What a new period is: a plan from one day to another (both included) and its windows.</summary>
public sealed record SubscriptionTerms(
    string PlanCode, SubscriptionKind Kind, DateOnly StartsOn, DateOnly EndsOn, int ExpiringWindowDays, int GraceDays);
