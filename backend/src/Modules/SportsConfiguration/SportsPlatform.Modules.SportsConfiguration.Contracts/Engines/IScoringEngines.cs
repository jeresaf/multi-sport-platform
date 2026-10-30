using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>The scoring engines by family, and the generic fold every family shares (sport-engine.md §4).</summary>
public interface IScoringEngines
{
    /// <summary>
    /// The engine for the definition's <c>scoring_family</c>. A published definition always has one (the validator
    /// refuses a family without an engine), so a missing one is a bug and throws.
    /// </summary>
    IScoringEngine EngineFor(SportDefinition definition);

    /// <summary>
    /// The contest's live state from its whole timeline, in any order: each event's effective status (sport-engine.md
    /// §3), then the active events folded in sequence order into the score, the phase and the personnel.
    /// </summary>
    ContestLiveState Fold(ContestContext ctx, ContestStatus status, IReadOnlyList<TimelineEvent> timeline);
}
