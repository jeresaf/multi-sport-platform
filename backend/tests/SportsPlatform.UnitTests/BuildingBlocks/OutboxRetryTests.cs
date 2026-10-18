using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class OutboxRetryTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(8, 256)]
    [InlineData(9, 300)]
    [InlineData(40, 300)]
    public void Delay_doubles_and_is_capped_at_five_minutes(int failedAttempts, int seconds)
    {
        OutboxRetry.Delay(failedAttempts).ShouldBe(TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public void Delay_needs_at_least_one_failed_attempt()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => OutboxRetry.Delay(0));
    }
}
