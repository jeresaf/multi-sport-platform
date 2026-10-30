namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// An event the scorer wants to record (sport-engine.md §3), before Match Operations gives it an id, a sequence and its
/// period: the current one, or for a correction (<see cref="SupersedesEventId"/>) the period and occurrence of the
/// event its chain of corrections started from. <see cref="PeriodCode"/> is only for <c>PERIOD_START</c>: the period
/// it starts.
/// </summary>
public sealed record TimelineEventDraft(string EventType, TimelineSource Source)
{
    /// <summary>The acting participant; null for events of the contest as a whole (<c>PERIOD_START</c>).</summary>
    public Guid? ParticipantId { get; init; }

    public string? PeriodCode { get; init; }

    public int? GameClockSeconds { get; init; }

    public int? AddedTimeSeconds { get; init; }

    /// <summary>Athlete or staff id by actor role (<c>SCORER</c>).</summary>
    public IReadOnlyDictionary<string, Guid> Actors { get; init; } = new Dictionary<string, Guid>();

    /// <summary>Metadata values by field, as text (<c>true</c>, <c>HEAD</c>, <c>3</c>).</summary>
    public IReadOnlyDictionary<string, string> Payload { get; init; } = new Dictionary<string, string>();

    public Guid? SupersedesEventId { get; init; }

    public Guid? ReversesEventId { get; init; }
}

/// <summary>
/// A recorded event: the draft's fields with its id, its sequence (gapless per contest, the authoritative order) and the
/// period it happened in. Match Operations maps its timeline rows to these from S12.
/// </summary>
public sealed record TimelineEvent(Guid Id, long Sequence, string EventType, TimelineSource Source)
{
    public Guid? ParticipantId { get; init; }

    /// <summary>
    /// The period the event belongs to (for <c>PERIOD_START</c>, the one it starts; for a correction, that of the event
    /// its chain of corrections started from); null before any.
    /// </summary>
    public string? PeriodCode { get; init; }

    /// <summary>Which occurrence of <see cref="PeriodCode"/> (a repeatable overtime's second occurrence is 2).</summary>
    public int PeriodOccurrence { get; init; }

    public int? GameClockSeconds { get; init; }

    public int? AddedTimeSeconds { get; init; }

    public IReadOnlyDictionary<string, Guid> Actors { get; init; } = new Dictionary<string, Guid>();

    public IReadOnlyDictionary<string, string> Payload { get; init; } = new Dictionary<string, string>();

    public Guid? SupersedesEventId { get; init; }

    public Guid? ReversesEventId { get; init; }
}

/// <summary>Where an event came from (sport-engine.md §3).</summary>
public enum TimelineSource
{
    Live,
    OfflineSync,
    Backfill,
    Provider,
    Admin,
}

/// <summary>Derived, never stored: <c>REVERSED</c>, else <c>SUPERSEDED</c>, else <c>ACTIVE</c> (sport-engine.md §3).</summary>
public enum EffectiveStatus
{
    Active,
    Superseded,
    Reversed,
}
