using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform;

/// <summary>The Platform module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class PlatformErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "PLATFORM_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error OutboxMessageNotFound = new(
        "PLATFORM_OUTBOX_MESSAGE_NOT_FOUND", "No outbox message with this ID in this module.", ErrorKind.NotFound);

    public static readonly Error OutboxMessageNotFailed = new(
        "PLATFORM_OUTBOX_MESSAGE_NOT_FAILED", "Only a FAILED outbox message can be re-driven.", ErrorKind.Conflict);
}
