using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using static SportsPlatform.UnitTests.Tenancy.SubscriptionPeriodTests;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>A tenant's periods (S04 D3, D4): the current period, no overlaps, and assign, renew, change plan and cancel.</summary>
public sealed class TenantSubscriptionTests
{
    private static readonly DateOnly _today = new(2026, 10, 23);
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Dictionary<string, int> _ranks = new() { ["STANDARD"] = 1, ["PRO"] = 2 };

    [Fact]
    public void Without_periods_there_is_no_current_period()
    {
        var subscription = new TenantSubscription([]);

        subscription.CurrentOn(_today).ShouldBeNull();
        subscription.NextScheduledOn(_today).ShouldBeNull();
    }

    [Fact]
    public void The_current_period_is_the_latest_started_one_that_is_not_cancelled()
    {
        var expired = Period("2025-01-01", "2025-12-31");
        var running = Period("2026-01-01", "2026-12-31");
        var next = Period("2027-01-01", "2027-12-31");
        var subscription = new TenantSubscription([next, expired, running]);

        subscription.CurrentOn(_today).ShouldBeSameAs(running);
        subscription.NextScheduledOn(_today).ShouldBeSameAs(next);
        subscription.Periods.ShouldBe([next, running, expired]);
    }

    [Fact]
    public void An_expired_period_stays_current_so_it_says_why()
    {
        var expired = Period("2025-01-01", "2025-12-31");

        new TenantSubscription([expired]).CurrentOn(_today).ShouldBeSameAs(expired);
    }

    [Fact]
    public void With_every_period_cancelled_the_latest_is_current()
    {
        var older = Period("2025-01-01", "2025-12-31");
        var cancelled = Period("2026-01-01", "2026-12-31");
        older.Cancel("Assigned on the wrong plan", SystemActor.Instance, _now);
        cancelled.Cancel("Association closed", SystemActor.Instance, _now);

        new TenantSubscription([older, cancelled]).CurrentOn(_today).ShouldBeSameAs(cancelled);
    }

    [Fact]
    public void A_cancelled_period_gives_way_to_an_earlier_one_that_is_not()
    {
        var earlier = Period("2026-01-01", "2026-12-31");
        var cancelled = Period("2026-06-01", "2026-06-30");
        cancelled.Cancel("Assigned by mistake", SystemActor.Instance, _now);

        new TenantSubscription([earlier, cancelled]).CurrentOn(_today).ShouldBeSameAs(earlier);
    }

    [Fact]
    public void With_only_a_scheduled_period_it_is_current_and_next()
    {
        var scheduled = Period("2026-11-01", "2027-04-30");
        var subscription = new TenantSubscription([scheduled]);

        subscription.CurrentOn(_today).ShouldBeSameAs(scheduled);
        subscription.NextScheduledOn(_today).ShouldBeSameAs(scheduled);
    }

    [Fact]
    public void Assign_adds_a_period_next_to_another()
    {
        var subscription = new TenantSubscription([Period("2026-01-01", "2026-12-31")]);

        var added = subscription.Assign(Terms("2027-01-01", "2027-12-31"));

        added.IsSuccess.ShouldBeTrue();
        subscription.Periods.Count.ShouldBe(2);
        subscription.NextScheduledOn(_today).ShouldBeSameAs(added.Value);
    }

    [Theory]
    [InlineData("2026-12-31", "2027-06-30")]
    [InlineData("2025-06-01", "2026-01-01")]
    [InlineData("2026-03-01", "2026-03-31")]
    public void Assign_refuses_a_period_that_shares_a_day(string startsOn, string endsOn)
    {
        var subscription = new TenantSubscription([Period("2026-01-01", "2026-12-31")]);

        var added = subscription.Assign(Terms(startsOn, endsOn));

        added.Error.ShouldBe(TenancyErrors.SubscriptionOverlap);
        subscription.Periods.Count.ShouldBe(1);
    }

    [Fact]
    public void Assign_ignores_cancelled_periods()
    {
        var cancelled = Period("2026-01-01", "2026-12-31");
        cancelled.Cancel("Assigned on the wrong plan", SystemActor.Instance, _now);

        new TenantSubscription([cancelled]).Assign(Terms("2026-01-01", "2026-12-31")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Renew_starts_the_day_after_the_latest_period_ends_and_keeps_its_terms()
    {
        var running = Period("2026-01-01", "2026-12-31", planCode: "PRO", expiringWindowDays: 20, graceDays: 7);
        var subscription = new TenantSubscription([running]);

        var renewed = subscription.Renew(new DateOnly(2027, 12, 31), null, null, null, null, _today).Value;

        renewed.StartsOn.ShouldBe(new DateOnly(2027, 1, 1));
        renewed.EndsOn.ShouldBe(new DateOnly(2027, 12, 31));
        renewed.PlanCode.ShouldBe("PRO");
        renewed.Kind.ShouldBe(SubscriptionKind.Paid);
        renewed.ExpiringWindowDays.ShouldBe(20);
        renewed.GraceDays.ShouldBe(7);
    }

    [Fact]
    public void Renew_follows_a_scheduled_period()
    {
        var subscription = new TenantSubscription([Period("2026-01-01", "2026-12-31"), Period("2027-01-01", "2027-06-30")]);

        subscription.Renew(new DateOnly(2027, 12, 31), null, null, null, null, _today).Value.StartsOn.ShouldBe(new DateOnly(2027, 7, 1));
    }

    [Fact]
    public void Renew_after_the_end_starts_today_and_takes_the_terms_sent()
    {
        var subscription = new TenantSubscription([Period("2025-01-01", "2025-12-31", planCode: "PRO")]);

        var renewed = subscription.Renew(new DateOnly(2027, 10, 22), "STANDARD", SubscriptionKind.Complimentary, 10, 0, _today).Value;

        renewed.StartsOn.ShouldBe(_today);
        renewed.PlanCode.ShouldBe("STANDARD");
        renewed.Kind.ShouldBe(SubscriptionKind.Complimentary);
        renewed.ExpiringWindowDays.ShouldBe(10);
        renewed.GraceDays.ShouldBe(0);
    }

    [Fact]
    public void Renew_after_a_cancel_starts_today()
    {
        var cancelled = Period("2026-01-01", "2026-12-31");
        cancelled.Cancel("Fees unpaid", SystemActor.Instance, _now);

        new TenantSubscription([cancelled]).Renew(new DateOnly(2027, 10, 22), null, null, null, null, _today).Value.StartsOn.ShouldBe(_today);
    }

    [Fact]
    public void Renew_without_any_period_is_refused()
    {
        new TenantSubscription([]).Renew(new DateOnly(2027, 10, 22), null, null, null, null, _today).Error
            .ShouldBe(TenancyErrors.SubscriptionNotFound);
    }

    [Fact]
    public void Renew_to_a_day_before_its_first_day_is_an_invalid_request()
    {
        var subscription = new TenantSubscription([Period("2026-01-01", "2026-12-31")]);

        subscription.Renew(new DateOnly(2026, 12, 31), null, null, null, null, _today).Error.ShouldBe(CommonErrors.InvalidRequest);
    }

    [Fact]
    public void Change_plan_cancels_the_current_period_and_adds_one_from_today_to_the_same_end()
    {
        var running = Period("2026-01-01", "2026-12-31", expiringWindowDays: 20, graceDays: 7);
        var subscription = new TenantSubscription([running]);

        var changed = subscription.ChangePlan("PRO", Rank, SystemActor.Instance, _now, _today).Value;

        running.StatusOn(_today).ShouldBe(SubscriptionStatus.Cancelled);
        running.CancelReason.ShouldBe("Changed to PRO");
        changed.PlanCode.ShouldBe("PRO");
        changed.StartsOn.ShouldBe(_today);
        changed.EndsOn.ShouldBe(running.EndsOn);
        changed.ExpiringWindowDays.ShouldBe(20);
        changed.GraceDays.ShouldBe(7);
        subscription.CurrentOn(_today).ShouldBeSameAs(changed);
        subscription.Periods.Count.ShouldBe(2);
    }

    [Fact]
    public void Change_plan_on_the_first_day_of_a_period_works()
    {
        var subscription = new TenantSubscription([Period("2026-10-23", "2027-10-22")]);

        subscription.ChangePlan("PRO", Rank, SystemActor.Instance, _now, _today).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("STANDARD")]
    [InlineData("BASIC")]
    public void Change_plan_to_the_same_or_a_lower_plan_waits_for_the_renewal(string planCode)
    {
        var running = Period("2026-01-01", "2026-12-31");
        var ranks = new Dictionary<string, int>(_ranks) { ["BASIC"] = 0 };
        var subscription = new TenantSubscription([running]);

        subscription.ChangePlan(planCode, code => ranks[code], SystemActor.Instance, _now, _today).Error
            .ShouldBe(TenancyErrors.SubscriptionDowngradeAtRenewal);
        running.IsCancelled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2025-10-01", "2026-10-15")]
    [InlineData("2025-01-01", "2025-12-31")]
    [InlineData("2026-11-01", "2027-10-31")]
    public void Change_plan_needs_a_period_in_effect_today(string startsOn, string endsOn)
    {
        var subscription = new TenantSubscription([Period(startsOn, endsOn)]);

        subscription.ChangePlan("PRO", Rank, SystemActor.Instance, _now, _today).Error.ShouldBe(TenancyErrors.SubscriptionNotFound);
    }

    [Fact]
    public void Change_plan_without_any_period_is_refused()
    {
        new TenantSubscription([]).ChangePlan("PRO", Rank, SystemActor.Instance, _now, _today).Error
            .ShouldBe(TenancyErrors.SubscriptionNotFound);
    }

    [Fact]
    public void Cancel_cancels_the_current_and_every_scheduled_period_but_not_the_past()
    {
        var past = Period("2025-01-01", "2025-12-31");
        var running = Period("2026-01-01", "2026-12-31");
        var next = Period("2027-01-01", "2027-12-31");
        var subscription = new TenantSubscription([past, running, next]);

        var cancelled = subscription.Cancel("Association merged into another", SystemActor.Instance, _now, _today).Value;

        cancelled.ShouldBe([running, next]);
        past.IsCancelled.ShouldBeFalse();
        running.CancelReason.ShouldBe("Association merged into another");
        next.StatusOn(_today).ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_in_grace_cancels_the_period()
    {
        var inGrace = Period("2025-10-20", "2026-10-15");

        new TenantSubscription([inGrace]).Cancel("Not renewing", SystemActor.Instance, _now, _today).Value.ShouldBe([inGrace]);
    }

    [Fact]
    public void Cancel_with_an_expired_current_period_cancels_only_the_scheduled_one()
    {
        var expired = Period("2025-01-01", "2025-12-31");
        var next = Period("2026-11-01", "2027-10-31");

        new TenantSubscription([expired, next]).Cancel("Not renewing", SystemActor.Instance, _now, _today).Value.ShouldBe([next]);
        expired.IsCancelled.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_with_nothing_to_cancel_is_refused()
    {
        var cancelled = Period("2026-01-01", "2026-12-31");
        cancelled.Cancel("Fees unpaid", SystemActor.Instance, _now);

        new TenantSubscription([]).Cancel("Not renewing", SystemActor.Instance, _now, _today).Error.ShouldBe(TenancyErrors.SubscriptionNotFound);
        new TenantSubscription([Period("2025-01-01", "2025-12-31")]).Cancel("Not renewing", SystemActor.Instance, _now, _today).Error
            .ShouldBe(TenancyErrors.SubscriptionNotFound);
        new TenantSubscription([cancelled]).Cancel("Not renewing", SystemActor.Instance, _now, _today).Error
            .ShouldBe(TenancyErrors.SubscriptionNotFound);
    }

    private static int? Rank(string code) => _ranks.TryGetValue(code, out var rank) ? rank : null;

    private static SubscriptionTerms Terms(string startsOn, string endsOn) =>
        new("STANDARD", SubscriptionKind.Paid, DateOnly.Parse(startsOn, null), DateOnly.Parse(endsOn, null), 30, 14);
}
