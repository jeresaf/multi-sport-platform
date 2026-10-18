namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// When a module's outbox can't be processed (database down, schema not migrated), the processor skips that module
/// for 1 s, 2 s, 4 s … up to 30 s instead of failing (and logging) on every poll; the first success resets it.
/// </summary>
public sealed class OutboxModuleBackoff
{
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;

    public int ConsecutiveFailures { get; private set; }

    public bool IsDue(DateTimeOffset now) => now >= _retryAt;

    /// <summary>Counts a failure and returns how long the module is now skipped.</summary>
    public TimeSpan RecordFailure(DateTimeOffset now)
    {
        ConsecutiveFailures++;
        var delay = ConsecutiveFailures > 5 ? MaxDelay : TimeSpan.FromSeconds(1 << (ConsecutiveFailures - 1));
        _retryAt = now + delay;
        return delay;
    }

    /// <summary>Resets after a success; true if the module had been failing.</summary>
    public bool RecordSuccess()
    {
        var recovered = ConsecutiveFailures > 0;
        ConsecutiveFailures = 0;
        _retryAt = DateTimeOffset.MinValue;
        return recovered;
    }
}
