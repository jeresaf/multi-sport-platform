using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration;

/// <summary>The Sports Configuration module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class SportsConfigurationErrors
{
    // Configuration rows are written only by seeders; ModuleDbContext needs the module's conflict all the same.
    public static readonly Error ConcurrencyConflict = new(
        "SPORT_CONCURRENCY_CONFLICT", "The configuration was changed by someone else. Reload and try again.", ErrorKind.Conflict);
}
