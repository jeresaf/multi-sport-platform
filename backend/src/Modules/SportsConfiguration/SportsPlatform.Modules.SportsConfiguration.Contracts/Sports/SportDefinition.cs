using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

/// <summary>
/// A published sport definition, resolved: the file's content (docs/domain/sports/README.md, schema version 1) as typed,
/// read-only records, with the generic event types every definition has and the stored hash. Members follow the file's
/// keys (<c>event_types</c> → <see cref="EventTypes"/>); optional lists are empty when the file leaves them out.
/// </summary>
public sealed record SportDefinition(
    int SchemaVersion,
    string Variant,
    string Sport,
    int Version,
    string Name,
    string ScoringFamily,
    ContestShape Shape,
    IReadOnlyList<string> ParticipantTypes,
    IReadOnlyList<CoverageLevel> CoverageLevels,
    SportTerminology Terminology,
    SportSupports Supports,
    IReadOnlyList<PeriodDefinition> Periods,
    IReadOnlyDictionary<string, ActorRoleDefinition> ActorRoles,
    IReadOnlyList<EventTypeDefinition> EventTypes,
    IReadOnlyList<PositionDefinition> Positions,
    IReadOnlyList<StaffRoleDefinition> StaffRoles,
    IReadOnlyList<OfficialRoleDefinition> OfficialRoles,
    LineupDefinition Lineup,
    IReadOnlyList<StatisticDefinition> Statistics,
    ScorerPadDefinition ScorerPad)
{
    /// <summary>The sides of a head-to-head contest (<c>HOME</c>, <c>AWAY</c>); empty for a field contest.</summary>
    public IReadOnlyList<string> Sides { get; init; } = [];

    /// <summary>The event types every definition has without listing them (README.md, "Generic event types").</summary>
    [JsonIgnore]
    public IReadOnlyList<GenericEventTypeDefinition> GenericEventTypes { get; init; } = [];

    /// <summary><c>sha256:</c> and the hex SHA-256 of the content in RFC 8785 canonical JSON (S05 D4).</summary>
    [JsonIgnore]
    public string ContentHash { get; init; } = string.Empty;

    [JsonIgnore]
    public DateTimeOffset PublishedAt { get; init; }
}

public sealed record SportTerminology(
    string Contest,
    string ContestPlural,
    string Period,
    string Participant,
    string Athlete,
    string ScoreUnit);

public sealed record SportSupports(IReadOnlyList<string> TieResolutions);

/// <summary>A period; <see cref="ShortLabel"/> is the file's <c>short</c> (<c>1H</c>).</summary>
public sealed record PeriodDefinition(
    string Code,
    string Label,
    [property: JsonPropertyName("short")] string ShortLabel,
    PhaseKind Kind,
    PeriodClock Clock)
{
    public int? DefaultSeconds { get; init; }

    public int? ClockStartsAtSeconds { get; init; }

    public bool AllowsAddedTime { get; init; }

    public bool Overtime { get; init; }

    public bool Repeatable { get; init; }

    public bool SuddenDeath { get; init; }
}

public sealed record ActorRoleDefinition(ActorPool Who, ActorSide Side);

public sealed record EventTypeDefinition(
    string Code,
    EventCategory Category,
    EventImportance Importance,
    CoverageLevel Coverage,
    string Label,
    IReadOnlyList<PhaseKind> Phases)
{
    public int? Points { get; init; }

    public ActorSide? Side { get; init; }

    /// <summary>The score component the points go to (<c>main</c>, <c>shootout</c>); null when it scores nothing.</summary>
    public string? Component { get; init; }

    public IReadOnlyList<EventActorDefinition> Actors { get; init; } = [];

    public IReadOnlyDictionary<string, MetadataFieldDefinition> Metadata { get; init; } = new Dictionary<string, MetadataFieldDefinition>();

    public IReadOnlyList<EventEffect> Effects { get; init; } = [];

    public FollowUpDefinition? FollowUp { get; init; }

    public IReadOnlyList<EventLimitDefinition> Limits { get; init; } = [];

    public IReadOnlyList<string> ReversibleBy { get; init; } = [];
}

public sealed record EventActorDefinition(string Role)
{
    public CoverageLevel? RequiredFrom { get; init; }

    public bool Optional { get; init; }

    public string? DistinctFrom { get; init; }
}

public sealed record MetadataFieldDefinition(MetadataFieldType Type)
{
    public IReadOnlyList<string> Values { get; init; } = [];

    public long? Min { get; init; }

    public long? Max { get; init; }

    public bool Optional { get; init; }
}

public sealed record FollowUpDefinition(IReadOnlyList<string> RequiresPreceding)
{
    public bool SameSide { get; init; }

    public bool SamePeriod { get; init; }

    public bool Unconsumed { get; init; }

    public bool Consumes { get; init; }
}

/// <summary>A limit the ruleset sets, e.g. <c>substitutions.max</c>.</summary>
public sealed record EventLimitDefinition(string Rule);

/// <summary>A generic event type: its code, category and an English label (README.md gives no other fields).</summary>
public sealed record GenericEventTypeDefinition(string Code, EventCategory Category, string Label);

public sealed record PositionDefinition(string Code, string Label, string Group);

public sealed record StaffRoleDefinition(string Code, string Label);

public sealed record OfficialRoleDefinition(string Code, string Label, int DefaultMinimum)
{
    public int? Max { get; init; }
}

public sealed record LineupDefinition(int Starters, int BenchMax)
{
    public IReadOnlyList<PositionConstraintDefinition> PositionConstraints { get; init; } = [];

    public IReadOnlyList<string> SpecialRoles { get; init; } = [];

    public int? MinimumPlayersToContinue { get; init; }

    public bool ShirtNumbersFollowPositions { get; init; }
}

/// <summary>A lineup rule on a position group or a single position code.</summary>
public sealed record PositionConstraintDefinition(string Group)
{
    public int? Min { get; init; }

    public int? Max { get; init; }
}

public sealed record StatisticDefinition(
    string Code,
    string Label,
    IReadOnlyList<string> Scopes,
    StatisticAggregation Aggregation,
    bool HigherIsBetter,
    CoverageLevel Coverage,
    StatisticDerivation Derive);

public sealed record StatisticDerivation
{
    public IReadOnlyList<string> CountEvents { get; init; } = [];

    public IReadOnlyList<string> SumPoints { get; init; } = [];

    public IReadOnlyList<string> ActorRoles { get; init; } = [];

    /// <summary>Metadata conditions on the counted events, each value as written (<c>true</c>, <c>OFFENSIVE</c>, <c>3</c>).</summary>
    public IReadOnlyDictionary<string, string> Where { get; init; } = new Dictionary<string, string>();

    public string? Builtin { get; init; }

    public string? Numerator { get; init; }

    public string? Denominator { get; init; }

    public bool Manual { get; init; }
}

public sealed record ScorerPadDefinition(ScorerPadKind Kind, IReadOnlyList<ScorerPadGroup> Groups);

public sealed record ScorerPadGroup(string Label, IReadOnlyList<string> Buttons)
{
    public IReadOnlyList<PhaseKind> Phases { get; init; } = [];

    public CoverageLevel? Coverage { get; init; }
}
