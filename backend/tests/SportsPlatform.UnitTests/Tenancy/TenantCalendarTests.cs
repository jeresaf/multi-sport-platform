using Microsoft.Extensions.Time.Testing;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>Subscription days begin at midnight in the tenant's time zone, not in UTC (S04 D3).</summary>
public sealed class TenantCalendarTests
{
    [Theory]
    [InlineData("2026-10-31T20:59:59Z", "Africa/Kampala", "2026-10-31")]
    [InlineData("2026-10-31T21:00:00Z", "Africa/Kampala", "2026-11-01")]
    [InlineData("2026-10-31T21:00:00Z", "UTC", "2026-10-31")]
    [InlineData("2026-11-01T00:00:00Z", "UTC", "2026-11-01")]
    public void Today_is_the_date_in_the_tenants_time_zone(string utcNow, string timeZone, string expected)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, null));

        TenantCalendar.Today(clock, timeZone).ShouldBe(DateOnly.Parse(expected, null));
    }

    [Theory]
    [InlineData("2026-10-31T20:59:59Z", "Africa/Kampala", "2026-10-31")]
    [InlineData("2026-10-31T21:00:00Z", "Africa/Kampala", "2026-11-01")]
    [InlineData("2026-10-31T21:00:00Z", "UTC", "2026-10-31")]
    public void An_instant_falls_on_the_date_in_the_tenants_time_zone(string instant, string timeZone, string expected)
    {
        TenantCalendar.DayOf(DateTimeOffset.Parse(instant, null), timeZone).ShouldBe(DateOnly.Parse(expected, null));
    }
}
