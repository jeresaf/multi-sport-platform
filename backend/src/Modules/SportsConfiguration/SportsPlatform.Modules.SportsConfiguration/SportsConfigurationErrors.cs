using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration;

/// <summary>The Sports Configuration module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class SportsConfigurationErrors
{
    // Configuration rows are written only by seeders; ModuleDbContext needs the module's conflict all the same.
    public static readonly Error ConcurrencyConflict = new(
        "SPORT_CONCURRENCY_CONFLICT", "The configuration was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error SportDefinitionNotFound = new(
        "SPORT_DEFINITION_NOT_FOUND", "No published sport definition matches this variant, or this version of it.", ErrorKind.NotFound);

    // Timeline errors the engines return; the scorer sees them from S12 (S05 D8). No endpoint returns them yet.
    public static readonly Error TimelineEventUnknown = new(
        "TIMELINE_EVENT_UNKNOWN", "The sport definition has no event type with this code.", ErrorKind.Unprocessable);
}
