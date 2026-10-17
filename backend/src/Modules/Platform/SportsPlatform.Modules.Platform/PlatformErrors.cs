using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform;

/// <summary>The Platform module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class PlatformErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "PLATFORM_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload the page and try again.", ErrorKind.Conflict);
}
