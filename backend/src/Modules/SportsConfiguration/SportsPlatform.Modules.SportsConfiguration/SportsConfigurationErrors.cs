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

    public static readonly Error TimelineContestNotLive = new(
        "TIMELINE_CONTEST_NOT_LIVE", "Events can be recorded only while the contest is live, or within the correction window after it ended.", ErrorKind.Unprocessable);

    public static readonly Error TimelineEventNotAllowedInPhase = new(
        "TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE", "This event can't be recorded in the current phase of the contest.", ErrorKind.Unprocessable);

    public static readonly Error TimelineParticipantInvalid = new(
        "TIMELINE_PARTICIPANT_INVALID", "The event needs one of the contest's participants, and only one of them.", ErrorKind.Unprocessable);

    public static readonly Error TimelineActorRequired = new(
        "TIMELINE_ACTOR_REQUIRED", "The event needs a person in each of its required roles.", ErrorKind.Unprocessable);

    public static readonly Error TimelineActorNotOnField = new(
        "TIMELINE_ACTOR_NOT_ON_FIELD", "A person named on the event is not on the field.", ErrorKind.Unprocessable);

    public static readonly Error TimelineActorInvalid = new(
        "TIMELINE_ACTOR_INVALID", "A person named on the event can't fill that role.", ErrorKind.Unprocessable);

    public static readonly Error TimelineReferenceInvalid = new(
        "TIMELINE_REFERENCE_INVALID", "The event this one corrects or reverses doesn't exist, no longer stands, or can't be corrected or reversed this way.", ErrorKind.Unprocessable);
}
