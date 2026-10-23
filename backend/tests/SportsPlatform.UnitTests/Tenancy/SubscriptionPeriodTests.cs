using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>A period's derived status: every row of state-machines.md's "Subscription period" table, on its boundary days.</summary>
public sealed class SubscriptionPeriodTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    /// <summary>2026 on STANDARD with the default windows: EXPIRING from 2 December, GRACE until 14 January 2027.</summary>
    public static TheoryData<string, SubscriptionStatus> YearWithDefaultWindows() => new()
    {
        { "2025-12-31", SubscriptionStatus.Scheduled },
        { "2026-01-01", SubscriptionStatus.Active },
        { "2026-12-01", SubscriptionStatus.Active },
        { "2026-12-02", SubscriptionStatus.Expiring },
        { "2026-12-31", SubscriptionStatus.Expiring },
        { "2027-01-01", SubscriptionStatus.Grace },
        { "2027-01-14", SubscriptionStatus.Grace },
        { "2027-01-15", SubscriptionStatus.Expired },
    };

    [Theory]
    [MemberData(nameof(YearWithDefaultWindows))]
    public void Status_follows_the_dates_with_both_ends_included(string today, SubscriptionStatus expected)
    {
        var period = Period("2026-01-01", "2026-12-31");

        period.StatusOn(DateOnly.Parse(today, null)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2025-12-31")]
    [InlineData("2026-06-30")]
    [InlineData("2026-12-31")]
    [InlineData("2027-06-30")]
    public void A_cancelled_period_is_cancelled_on_every_day(string today)
    {
        var period = Period("2026-01-01", "2026-12-31");

        period.Cancel("Tenant closed its competitions", SystemActor.Instance, _now);

        period.StatusOn(DateOnly.Parse(today, null)).ShouldBe(SubscriptionStatus.Cancelled);
        period.CancelReason.ShouldBe("Tenant closed its competitions");
        period.CancelledAt.ShouldBe(_now);
        period.CancelledBy.ShouldBe(SystemActor.Instance.ActorId);
    }

    [Theory]
    [InlineData("2026-12-31", SubscriptionStatus.Active)]
    [InlineData("2027-01-01", SubscriptionStatus.Expired)]
    public void Without_windows_it_is_active_to_the_last_day_then_expired(string today, SubscriptionStatus expected)
    {
        var period = Period("2026-01-01", "2026-12-31", expiringWindowDays: 0, graceDays: 0);

        period.StatusOn(DateOnly.Parse(today, null)).ShouldBe(expected);
    }

    [Fact]
    public void A_period_shorter_than_its_window_is_expiring_from_its_first_day()
    {
        var period = Period("2026-11-01", "2026-11-10");

        period.StatusOn(new DateOnly(2026, 11, 1)).ShouldBe(SubscriptionStatus.Expiring);
        period.ExpiringFrom.ShouldBe(new DateOnly(2026, 10, 12));
        period.GraceEndsOn.ShouldBe(new DateOnly(2026, 11, 24));
    }

    [Theory]
    [InlineData("2025-12-01", "2025-12-31", false)]
    [InlineData("2025-12-01", "2026-01-01", true)]
    [InlineData("2026-03-01", "2026-03-31", true)]
    [InlineData("2026-12-31", "2027-01-31", true)]
    [InlineData("2027-01-01", "2027-12-31", false)]
    public void Overlap_means_sharing_a_day(string startsOn, string endsOn, bool overlaps)
    {
        var period = Period("2026-01-01", "2026-12-31");

        period.Overlaps(DateOnly.Parse(startsOn, null), DateOnly.Parse(endsOn, null)).ShouldBe(overlaps);
    }

    [Fact]
    public void A_cancelled_period_overlaps_nothing()
    {
        var period = Period("2026-01-01", "2026-12-31");
        period.Cancel("Assigned on the wrong plan", SystemActor.Instance, _now);

        period.Overlaps(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)).ShouldBeFalse();
    }

    [Fact]
    public void Cancelling_twice_is_a_bug()
    {
        var period = Period("2026-01-01", "2026-12-31");
        period.Cancel("Assigned on the wrong plan", SystemActor.Instance, _now);

        Should.Throw<InvalidOperationException>(() => period.Cancel("Again", SystemActor.Instance, _now));
    }

    [Fact]
    public void Dates_and_windows_out_of_range_are_bugs()
    {
        var day = new DateOnly(2026, 1, 1);
        Should.Throw<ArgumentOutOfRangeException>(() => SubscriptionPeriod.Create("STANDARD", SubscriptionKind.Paid, day, day.AddDays(-1), 30, 14));
        Should.Throw<ArgumentOutOfRangeException>(() => SubscriptionPeriod.Create("STANDARD", SubscriptionKind.Paid, day, day, -1, 14));
        Should.Throw<ArgumentOutOfRangeException>(() => SubscriptionPeriod.Create("STANDARD", SubscriptionKind.Paid, day, day, 30, 366));
        Should.Throw<ArgumentException>(() => SubscriptionPeriod.Create(" ", SubscriptionKind.Paid, day, day, 30, 14));
    }

    internal static SubscriptionPeriod Period(
        string startsOn, string endsOn, string planCode = "STANDARD", int expiringWindowDays = 30, int graceDays = 14) =>
        SubscriptionPeriod.Create(
            planCode, SubscriptionKind.Paid, DateOnly.Parse(startsOn, null), DateOnly.Parse(endsOn, null), expiringWindowDays, graceDays);
}
