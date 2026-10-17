namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>Configuration section <c>Outbox</c>.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Whether the hosted service polls. Off in <c>Testing</c>, where tests call the processor directly.</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Messages claimed per module per poll.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Failed attempts after which a message is <c>FAILED</c> and waits for a re-drive.</summary>
    public int MaxAttempts { get; set; } = 10;
}
