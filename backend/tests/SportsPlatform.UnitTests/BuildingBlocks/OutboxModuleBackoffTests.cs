using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class OutboxModuleBackoffTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Delay_doubles_from_one_second_and_is_capped_at_thirty()
    {
        var backoff = new OutboxModuleBackoff();

        var delays = Enumerable.Range(0, 8).Select(_ => backoff.RecordFailure(_now).TotalSeconds).ToList();

        delays.ShouldBe([1, 2, 4, 8, 16, 30, 30, 30]);
        backoff.ConsecutiveFailures.ShouldBe(8);
    }

    [Fact]
    public void Module_is_skipped_until_the_delay_has_passed()
    {
        var backoff = new OutboxModuleBackoff();
        backoff.IsDue(_now).ShouldBeTrue();

        backoff.RecordFailure(_now);
        backoff.RecordFailure(_now);

        backoff.IsDue(_now.AddSeconds(1)).ShouldBeFalse();
        backoff.IsDue(_now.AddSeconds(2)).ShouldBeTrue();
    }

    [Fact]
    public void Success_resets_and_reports_a_recovery_once()
    {
        var backoff = new OutboxModuleBackoff();
        backoff.RecordSuccess().ShouldBeFalse();

        backoff.RecordFailure(_now);
        backoff.RecordSuccess().ShouldBeTrue();
        backoff.RecordSuccess().ShouldBeFalse();

        backoff.IsDue(_now).ShouldBeTrue();
        backoff.RecordFailure(_now).ShouldBe(TimeSpan.FromSeconds(1));
    }
}
