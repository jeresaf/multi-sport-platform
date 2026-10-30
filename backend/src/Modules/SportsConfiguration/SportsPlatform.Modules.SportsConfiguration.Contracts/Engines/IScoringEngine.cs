namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// The engine of one scoring family (ADR-0003, docs/architecture/sport-engine.md §4): pure and deterministic, with
/// every input in its arguments (no I/O, clock or randomness). Pick one with <see cref="IScoringEngines"/>, never by
/// sport.
/// </summary>
public interface IScoringEngine
{
    /// <summary>The definition's <c>scoring_family</c> this engine computes (<c>ADDITIVE</c>).</summary>
    string Family { get; }

    ScoreState Initial(ContestContext ctx);

    /// <summary>Whether <paramref name="draft"/> may be appended to the timeline <paramref name="live"/> was folded from.</summary>
    ValidationResult Validate(ContestContext ctx, ContestLiveState live, TimelineEventDraft draft);

    /// <summary>The state after an active event; events that score nothing return the state unchanged.</summary>
    ScoreState Apply(ContestContext ctx, ScoreState state, TimelineEvent evt);

    OutcomeProposal ProposeOutcome(ContestContext ctx, ScoreState state);

    /// <summary>The family-neutral display model every client renders without sport logic.</summary>
    ScoreSummary Summarize(ContestContext ctx, ScoreState state);
}

/// <summary>A family's score state. Only its engine reads inside it; others use Summarize and ProposeOutcome.</summary>
public abstract record ScoreState(string Family);
