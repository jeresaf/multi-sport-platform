using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform;

/// <summary>The Platform module's expected failures. T3 exports them to the error catalogue.</summary>
public static class PlatformErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "PLATFORM_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload and try again.", ErrorKind.Conflict);
}
