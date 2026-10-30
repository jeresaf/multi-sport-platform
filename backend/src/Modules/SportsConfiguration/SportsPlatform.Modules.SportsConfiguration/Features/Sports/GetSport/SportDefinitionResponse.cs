using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;

/// <summary>
/// A published sport definition as the API shows it: the definition's content (docs/domain/sports/README.md) in the API's
/// names (camelCase, UPPER_SNAKE values), with its version's hash and publication time and the generic event types.
/// Actor roles and metadata fields stay maps keyed by their codes, as in the file.
/// </summary>
public sealed record SportDefinitionResponse(
    string Variant,
    string Sport,
    int Version,
    string Name,
    string ScoringFamily,
    ContestShape Shape,
    int SchemaVersion,
    string ContentHash,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> ParticipantTypes,
    IReadOnlyList<CoverageLevel> CoverageLevels,
    IReadOnlyList<string> Sides,
    SportTerminologyResponse Terminology,
    IReadOnlyList<string> TieResolutions,
    IReadOnlyList<PeriodResponse> Periods,
    IReadOnlyDictionary<string, ActorRoleResponse> ActorRoles,
    IReadOnlyList<EventTypeResponse> EventTypes,
    IReadOnlyList<GenericEventTypeResponse> GenericEventTypes,
    IReadOnlyList<PositionResponse> Positions,
    IReadOnlyList<StaffRoleResponse> StaffRoles,
    IReadOnlyList<OfficialRoleResponse> OfficialRoles,
    LineupResponse Lineup,
    IReadOnlyList<StatisticResponse> Statistics,
    ScorerPadResponse ScorerPad)
{
    public static SportDefinitionResponse From(SportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var terminology = definition.Terminology;
        var lineup = definition.Lineup;
        return new SportDefinitionResponse(
            definition.Variant,
            definition.Sport,
            definition.Version,
            definition.Name,
            definition.ScoringFamily,
            definition.Shape,
            definition.SchemaVersion,
            definition.ContentHash,
            definition.PublishedAt,
            definition.ParticipantTypes,
            definition.CoverageLevels,
            definition.Sides,
            new SportTerminologyResponse(
                terminology.Contest, terminology.ContestPlural, terminology.Period, terminology.Participant, terminology.Athlete, terminology.ScoreUnit),
            definition.Supports.TieResolutions,
            [.. definition.Periods.Select(PeriodResponse.From)],
            definition.ActorRoles.ToDictionary(entry => entry.Key, entry => new ActorRoleResponse(entry.Value.Who, entry.Value.Side), StringComparer.Ordinal),
            [.. definition.EventTypes.Select(EventTypeResponse.From)],
            [.. definition.GenericEventTypes.Select(type => new GenericEventTypeResponse(type.Code, type.Category, type.Label))],
            [.. definition.Positions.Select(position => new PositionResponse(position.Code, position.Label, position.Group))],
            [.. definition.StaffRoles.Select(role => new StaffRoleResponse(role.Code, role.Label))],
            [.. definition.OfficialRoles.Select(role => new OfficialRoleResponse(role.Code, role.Label, role.DefaultMinimum, role.Max))],
            new LineupResponse(
                lineup.Starters,
                lineup.BenchMax,
                [.. lineup.PositionConstraints.Select(constraint => new PositionConstraintResponse(constraint.Group, constraint.Min, constraint.Max))],
                lineup.SpecialRoles,
                lineup.MinimumPlayersToContinue,
                lineup.ShirtNumbersFollowPositions),
            [.. definition.Statistics.Select(StatisticResponse.From)],
            new ScorerPadResponse(
                definition.ScorerPad.Kind,
                [.. definition.ScorerPad.Groups.Select(group => new ScorerPadGroupResponse(group.Label, group.Buttons, group.Phases, group.Coverage))]));
    }
}

public sealed record SportTerminologyResponse(
    string Contest,
    string ContestPlural,
    string Period,
    string Participant,
    string Athlete,
    string ScoreUnit);

/// <summary>A period; <c>shortLabel</c> is the definition's short form (<c>1H</c>).</summary>
public sealed record PeriodResponse(
    string Code,
    string Label,
    string ShortLabel,
    PhaseKind Kind,
    PeriodClock Clock,
    int? DefaultSeconds,
    int? ClockStartsAtSeconds,
    bool AllowsAddedTime,
    bool Overtime,
    bool Repeatable,
    bool SuddenDeath)
{
    public static PeriodResponse From(PeriodDefinition period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return new PeriodResponse(
            period.Code, period.Label, period.ShortLabel, period.Kind, period.Clock, period.DefaultSeconds, period.ClockStartsAtSeconds,
            period.AllowsAddedTime, period.Overtime, period.Repeatable, period.SuddenDeath);
    }
}

public sealed record ActorRoleResponse(ActorPool Who, ActorSide Side);

public sealed record EventTypeResponse(
    string Code,
    EventCategory Category,
    EventImportance Importance,
    CoverageLevel Coverage,
    string Label,
    IReadOnlyList<PhaseKind> Phases,
    int? Points,
    ActorSide? Side,
    string? Component,
    IReadOnlyList<EventActorResponse> Actors,
    IReadOnlyDictionary<string, MetadataFieldResponse> Metadata,
    IReadOnlyList<EventEffect> Effects,
    FollowUpResponse? FollowUp,
    IReadOnlyList<string> Limits,
    IReadOnlyList<string> ReversibleBy)
{
    public static EventTypeResponse From(EventTypeDefinition type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return new EventTypeResponse(
            type.Code,
            type.Category,
            type.Importance,
            type.Coverage,
            type.Label,
            type.Phases,
            type.Points,
            type.Side,
            type.Component,
            [.. type.Actors.Select(actor => new EventActorResponse(actor.Role, actor.RequiredFrom, actor.Optional, actor.DistinctFrom))],
            type.Metadata.ToDictionary(entry => entry.Key, entry => MetadataFieldResponse.From(entry.Value), StringComparer.Ordinal),
            type.Effects,
            type.FollowUp is { } followUp
                ? new FollowUpResponse(followUp.RequiresPreceding, followUp.SameSide, followUp.SamePeriod, followUp.Unconsumed, followUp.Consumes)
                : null,
            [.. type.Limits.Select(limit => limit.Rule)],
            type.ReversibleBy);
    }
}

public sealed record EventActorResponse(string Role, CoverageLevel? RequiredFrom, bool Optional, string? DistinctFrom);

/// <summary>A metadata field: <c>values</c> for a choice, <c>min</c> and <c>max</c> for a number.</summary>
public sealed record MetadataFieldResponse(MetadataValueType Type, IReadOnlyList<string> Values, long? Min, long? Max, bool Optional)
{
    public static MetadataFieldResponse From(MetadataFieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(field);
        var type = field.Type switch
        {
            MetadataFieldType.Choice => MetadataValueType.Choice,
            MetadataFieldType.Flag => MetadataValueType.Flag,
            MetadataFieldType.Number => MetadataValueType.Number,
            MetadataFieldType.Text => MetadataValueType.Text,
            _ => throw new InvalidOperationException($"Unknown metadata field type {field.Type}."),
        };
        return new MetadataFieldResponse(type, field.Values, field.Min, field.Max, field.Optional);
    }
}

/// <summary>
/// A metadata field's type in the API (<c>CHOICE</c>, <c>FLAG</c>, <c>NUMBER</c>, <c>TEXT</c>); the files write
/// <c>enum</c>, <c>bool</c>, <c>int</c> and <c>string</c>.
/// </summary>
public enum MetadataValueType
{
    Choice,
    Flag,
    Number,
    Text,
}

public sealed record FollowUpResponse(IReadOnlyList<string> RequiresPreceding, bool SameSide, bool SamePeriod, bool Unconsumed, bool Consumes);

public sealed record GenericEventTypeResponse(string Code, EventCategory Category, string Label);

public sealed record PositionResponse(string Code, string Label, string Group);

public sealed record StaffRoleResponse(string Code, string Label);

public sealed record OfficialRoleResponse(string Code, string Label, int DefaultMinimum, int? Max);

public sealed record LineupResponse(
    int Starters,
    int BenchMax,
    IReadOnlyList<PositionConstraintResponse> PositionConstraints,
    IReadOnlyList<string> SpecialRoles,
    int? MinimumPlayersToContinue,
    bool ShirtNumbersFollowPositions);

public sealed record PositionConstraintResponse(string Group, int? Min, int? Max);

public sealed record StatisticResponse(
    string Code,
    string Label,
    IReadOnlyList<string> Scopes,
    StatisticAggregation Aggregation,
    bool HigherIsBetter,
    CoverageLevel Coverage,
    StatisticDerivationResponse Derive)
{
    public static StatisticResponse From(StatisticDefinition statistic)
    {
        ArgumentNullException.ThrowIfNull(statistic);
        var derive = statistic.Derive;
        return new StatisticResponse(
            statistic.Code,
            statistic.Label,
            statistic.Scopes,
            statistic.Aggregation,
            statistic.HigherIsBetter,
            statistic.Coverage,
            new StatisticDerivationResponse(
                derive.CountEvents, derive.SumPoints, derive.ActorRoles, derive.Where, derive.Builtin, derive.Numerator, derive.Denominator, derive.Manual));
    }
}

/// <summary>How a statistic is derived; <c>where</c> holds metadata conditions, each value as written.</summary>
public sealed record StatisticDerivationResponse(
    IReadOnlyList<string> CountEvents,
    IReadOnlyList<string> SumPoints,
    IReadOnlyList<string> ActorRoles,
    IReadOnlyDictionary<string, string> Where,
    string? Builtin,
    string? Numerator,
    string? Denominator,
    bool Manual);

public sealed record ScorerPadResponse(ScorerPadKind Kind, IReadOnlyList<ScorerPadGroupResponse> Groups);

public sealed record ScorerPadGroupResponse(string Label, IReadOnlyList<string> Buttons, IReadOnlyList<PhaseKind> Phases, CoverageLevel? Coverage);
