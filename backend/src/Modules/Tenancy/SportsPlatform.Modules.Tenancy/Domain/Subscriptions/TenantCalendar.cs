namespace SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

/// <summary>
/// The tenant's date: subscription days begin and end at midnight in the tenant's time zone (settings), by the app's
/// clock (S04 D3). Computed on every call, never cached.
/// </summary>
public static class TenantCalendar
{
    /// <summary>Today in <paramref name="timeZone"/>, an IANA ID from the reference data (e.g. <c>Africa/Kampala</c>).</summary>
    public static DateOnly Today(TimeProvider timeProvider, string timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return DayOf(timeProvider.GetUtcNow(), timeZone);
    }

    /// <summary>The tenant's date at <paramref name="instant"/> in <paramref name="timeZone"/>, e.g. the day a period was cancelled.</summary>
    public static DateOnly DayOf(DateTimeOffset instant, string timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }
}
