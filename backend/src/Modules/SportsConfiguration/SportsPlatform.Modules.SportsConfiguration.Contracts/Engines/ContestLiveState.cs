using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// A contest as its timeline leaves it (sport-engine.md §4): the stored status, the score, the phase, who is where, and
/// every event with its effective status, in sequence order.
/// </summary>
public sealed record ContestLiveState(
    ContestStatus Status,
    ScoreState Score,
    ContestPhase Phase,
    ContestPersonnel Personnel,
    IReadOnlyList<TimelineEntry> Timeline)
{
    /// <summary>
    /// Whether scorers may still record events after the contest <c>ENDED</c>. Engines have no clock, so the caller
    /// decides from the end time and the rules' correction window (Match Operations from S12) and sets it on the folded
    /// state; the fold leaves it false.
    /// </summary>
    public bool CorrectionWindowOpen { get; init; }
}

/// <summary>An event of the timeline and its effective status.</summary>
public sealed record TimelineEntry(TimelineEvent Event, EffectiveStatus Status);

/// <summary>
/// The current period from the active <c>PERIOD_START</c> and <c>PERIOD_END</c> events. <see cref="Kind"/> is the
/// running period's kind, <see cref="PhaseKind.Break"/> once a period has ended and before the next starts, and null
/// before the first one.
/// </summary>
public sealed record ContestPhase(string? PeriodCode, int Occurrence, bool Running, PhaseKind? Kind)
{
    public static ContestPhase NotStarted { get; } = new(null, 0, Running: false, Kind: null);

    /// <summary>Every period started so far, in the order they started.</summary>
    public IReadOnlyList<PlayedPeriod> Played { get; init; } = [];
}

public sealed record PlayedPeriod(string PeriodCode, int Occurrence);

/// <summary>Who is where for each participant, from the lineups and the events' effects (sport-engine.md §4).</summary>
public sealed record ContestPersonnel(IReadOnlyDictionary<Guid, ParticipantPersonnel> Participants)
{
    public ParticipantPersonnel For(Guid participantId) =>
        Participants.TryGetValue(participantId, out var personnel) ? personnel : ParticipantPersonnel.Empty;
}

/// <summary>
/// One participant's people. Everyone in the lineup is in exactly one of <see cref="OnField"/>, <see cref="Bench"/>,
/// <see cref="Suspended"/>, <see cref="TemporarilyReplaced"/>, <see cref="Replaced"/> (substituted, may not return)
/// and <see cref="Dismissed"/> (sent off or disqualified); staff stay in <see cref="Staff"/> unless dismissed.
/// </summary>
public sealed record ParticipantPersonnel(
    IReadOnlySet<Guid> OnField,
    IReadOnlySet<Guid> Bench,
    IReadOnlySet<Guid> Staff,
    IReadOnlySet<Guid> Suspended,
    IReadOnlySet<Guid> Replaced,
    IReadOnlySet<Guid> Dismissed)
{
    public static ParticipantPersonnel Empty { get; } = new(
        new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>());

    /// <summary>Players off for a temporary substitution, each with the player who replaced them.</summary>
    public IReadOnlyDictionary<Guid, Guid> TemporarilyReplaced { get; init; } = new Dictionary<Guid, Guid>();

    /// <summary>Events with the <c>FOUL_COUNT</c> effect per person; the limit is a rule (S16).</summary>
    public IReadOnlyDictionary<Guid, int> Fouls { get; init; } = new Dictionary<Guid, int>();
}
