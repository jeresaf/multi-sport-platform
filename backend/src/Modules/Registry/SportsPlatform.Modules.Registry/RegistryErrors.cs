using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry;

/// <summary>The Registry module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class RegistryErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "PERSON_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload and try again.", ErrorKind.Conflict);
}
