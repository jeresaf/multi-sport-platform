namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>Exponential backoff between delivery attempts: 2 s, 4 s, 8 s … capped at 5 minutes.</summary>
public static class OutboxRetry
{
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    /// <param name="failedAttempts">Attempts that have failed so far (at least 1).</param>
    public static TimeSpan Delay(int failedAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);
        // 2^9 s is already past the cap (and larger shifts would overflow).
        return failedAttempts >= 9 ? MaxDelay : TimeSpan.FromSeconds(1 << failedAttempts);
    }
}
