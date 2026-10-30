using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

// The closed vocabularies of sport-definition.schema.json, written UPPER_SNAKE in the files (ON_FIELD → OnField).

public enum ContestShape
{
    HeadToHead,
    Field,
}

/// <summary>In order: a level includes every level before it.</summary>
public enum CoverageLevel
{
    ResultOnly,
    Basic,
    Standard,
    Detailed,
}

public enum PhaseKind
{
    Play,
    Break,
    Shootout,
}

public enum PeriodClock
{
    CountUp,
    CountDown,
    None,
}

/// <summary>Who may fill an actor role.</summary>
public enum ActorPool
{
    OnField,
    Bench,
    Squad,
    Staff,
    SquadOrStaff,
}

/// <summary>Whose side, relative to the event's side.</summary>
public enum ActorSide
{
    Own,
    Opponent,
}

public enum EventCategory
{
    System,
    Period,
    Scoring,
    Discipline,
    Personnel,
    Play,
    Review,
    Stoppage,
    Statistic,
    Commentary,
    Correction,
}

public enum EventImportance
{
    Key,
    Standard,
    Detailed,
    Technical,
}

public enum EventEffect
{
    SendOff,
    TemporarySuspension,
    Substitute,
    TemporarySubstitute,
    Return,
    Disqualify,
    FoulCount,
}

/// <summary>Written <c>enum</c>, <c>bool</c>, <c>int</c> and <c>string</c> in the files.</summary>
public enum MetadataFieldType
{
    /// <summary>One of the field's <c>values</c>.</summary>
    [JsonStringEnumMemberName("enum")]
    Choice,

    [JsonStringEnumMemberName("bool")]
    Flag,

    /// <summary>A whole number within the field's <c>min</c> and <c>max</c>.</summary>
    [JsonStringEnumMemberName("int")]
    Number,

    [JsonStringEnumMemberName("string")]
    Text,
}

public enum StatisticAggregation
{
    Sum,
    Max,
    Min,
    Ratio,
    Builtin,
}

public enum ScorerPadKind
{
    Buttons,
    DeliveryPad,
}
