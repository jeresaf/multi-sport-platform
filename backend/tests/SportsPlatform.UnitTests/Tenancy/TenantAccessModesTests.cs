using Microsoft.Extensions.Time.Testing;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Access;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using static SportsPlatform.UnitTests.Tenancy.SubscriptionPeriodTests;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>The access mode from the tenant's status and its current period (state-machines.md, "Access mode"; S04 D8).</summary>
public sealed class TenantAccessModesTests
{
    private static readonly SubscriptionStatus?[] _everySubscription = [null, .. Enum.GetValues<SubscriptionStatus>()];

    /// <summary>Every tenant status with every current period status, and none, as state-machines.md's table says.</summary>
    public static TheoryData<TenantStatus, SubscriptionStatus?, TenantAccessMode> Table()
    {
        var table = new TheoryData<TenantStatus, SubscriptionStatus?, TenantAccessMode>();
        foreach (var subscription in _everySubscription)
        {
            // PROVISIONING: FULL for its owners and admins whatever the subscription (step 1 refuses everyone else).
            table.Add(TenantStatus.Provisioning, subscription, TenantAccessMode.Full);

            // ACTIVE: FULL while a period is in effect; READ_ONLY when expired, cancelled, not started yet, or none.
            table.Add(TenantStatus.Active, subscription, subscription is SubscriptionStatus.Active or SubscriptionStatus.Expiring or SubscriptionStatus.Grace
                ? TenantAccessMode.Full
                : TenantAccessMode.ReadOnly);

            // SUSPENDED, ARCHIVED: BLOCKED whatever the subscription.
            table.Add(TenantStatus.Suspended, subscription, TenantAccessMode.Blocked);
            table.Add(TenantStatus.Archived, subscription, TenantAccessMode.Blocked);
        }

        return table;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void The_access_mode_follows_the_table(TenantStatus status, SubscriptionStatus? current, TenantAccessMode expected)
    {
        TenantAccessModes.Of(status, current).ShouldBe(expected);
    }

    /// <summary>
    /// The days begin at midnight in the tenant's time zone (Kampala, UTC+3: 21:00 UTC the day before). A period from
    /// 1 May to 31 October 2026, expiring from 2 October (30 days), grace until 14 November (14 days).
    /// </summary>
    [Theory]
    [InlineData("2026-04-30T20:59:59Z", SubscriptionStatus.Scheduled, TenantAccessMode.ReadOnly)]
    [InlineData("2026-04-30T21:00:00Z", SubscriptionStatus.Active, TenantAccessMode.Full)]
    [InlineData("2026-10-01T20:59:59Z", SubscriptionStatus.Active, TenantAccessMode.Full)]
    [InlineData("2026-10-01T21:00:00Z", SubscriptionStatus.Expiring, TenantAccessMode.Full)]
    [InlineData("2026-10-31T20:59:59Z", SubscriptionStatus.Expiring, TenantAccessMode.Full)]
    [InlineData("2026-10-31T21:00:00Z", SubscriptionStatus.Grace, TenantAccessMode.Full)]
    [InlineData("2026-11-14T20:59:59Z", SubscriptionStatus.Grace, TenantAccessMode.Full)]
    [InlineData("2026-11-14T21:00:00Z", SubscriptionStatus.Expired, TenantAccessMode.ReadOnly)]
    public void An_active_tenants_access_changes_at_midnight_in_its_time_zone(
        string utcNow, SubscriptionStatus status, TenantAccessMode mode)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, null));
        var period = Period("2026-05-01", "2026-10-31");
        var today = TenantCalendar.Today(clock, "Africa/Kampala");

        var current = new TenantSubscription([period]).CurrentOn(today).ShouldNotBeNull();

        current.StatusOn(today).ShouldBe(status);
        TenantAccessModes.Of(TenantStatus.Active, current.StatusOn(today)).ShouldBe(mode);
    }

    [Fact]
    public void A_cancellation_makes_an_active_tenant_read_only_the_same_day()
    {
        var period = Period("2026-05-01", "2026-10-31");
        period.Cancel(
            "Association stopped running competitions", SystemActor.Instance, new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero));

        var current = new TenantSubscription([period]).CurrentOn(new DateOnly(2026, 6, 1)).ShouldNotBeNull();

        TenantAccessModes.Of(TenantStatus.Active, current.StatusOn(new DateOnly(2026, 6, 1))).ShouldBe(TenantAccessMode.ReadOnly);
    }
}
