using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;

/// <summary>
/// A published ruleset template as the API shows it (docs/domain/rulesets.md): camelCase names, UPPER_SNAKE values, with
/// its version's hash and publication time. <c>parameters</c> are keyed by their dotted path into the rules as the files
/// write it (<c>standings.points.win</c>, <c>contest.period_seconds.FIRST_HALF</c>); maps keyed by codes keep the codes.
/// <c>tiebreakers</c> describes every tiebreaker the template lists or allows, in the catalogue's order.
/// </summary>
public sealed record RulesetTemplateResponse(
    string Code,
    int Version,
    string Name,
    string? Description,
    int SchemaVersion,
    string ContentHash,
    DateTimeOffset PublishedAt,
    RulesetSportResponse Sport,
    IReadOnlyList<string> Formats,
    IReadOnlyDictionary<string, RulesetParameterResponse> Parameters,
    RulesetValuesResponse Values,
    IReadOnlyList<TiebreakerResponse> Tiebreakers)
{
    public static RulesetTemplateResponse From(RulesetTemplate template, IReadOnlyList<TiebreakerDefinition> catalogue)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(catalogue);
        var used = template.Values.Standings.Tiebreakers
            .Concat(template.Editable.GetValueOrDefault(RulesetValueChecks.StandingsTiebreakersPath)?.Values ?? [])
            .ToHashSet(StringComparer.Ordinal);
        return new RulesetTemplateResponse(
            template.Code,
            template.Version,
            template.Name,
            template.Description,
            template.SchemaVersion,
            template.ContentHash,
            template.PublishedAt,
            new RulesetSportResponse(template.Sport.Variant, template.Sport.Version),
            template.Formats,
            template.Editable.ToDictionary(parameter => parameter.Key, parameter => RulesetParameterResponse.From(parameter.Value), StringComparer.Ordinal),
            RulesetValuesResponse.From(template.Values),
            [.. catalogue.Where(tiebreaker => used.Contains(tiebreaker.Code)).Select(tiebreaker => new TiebreakerResponse(tiebreaker.Code, tiebreaker.Label, tiebreaker.Description))]);
    }
}

/// <summary>The sport definition the template is written for, at the exact version.</summary>
public sealed record RulesetSportResponse(string Variant, int Version);

/// <summary>A tiebreaker of the catalogue.</summary>
public sealed record TiebreakerResponse(string Code, string Label, string Description);

/// <summary>
/// A parameter a competition may change: <c>min</c> and <c>max</c> for a number; <c>values</c> for a choice or a list
/// (the codes a list may hold, each once, in any order); empty otherwise. <c>default</c> is the template's value.
/// </summary>
public sealed record RulesetParameterResponse(
    RulesetParameterType Type,
    string Label,
    string? Description,
    int? Min,
    int? Max,
    IReadOnlyList<string> Values,
    ParameterValue Default)
{
    public static RulesetParameterResponse From(ParameterDefinition parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        var type = parameter.Type switch
        {
            ParameterType.Number => RulesetParameterType.Number,
            ParameterType.Flag => RulesetParameterType.Flag,
            ParameterType.Choice => RulesetParameterType.Choice,
            ParameterType.List => RulesetParameterType.List,
            _ => throw new InvalidOperationException($"Unknown parameter type {parameter.Type}."),
        };
        return new RulesetParameterResponse(type, parameter.Label, parameter.Description, parameter.Min, parameter.Max, parameter.Values, parameter.Default);
    }
}

/// <summary>A template parameter's type in the API; the files write <c>enum</c>, <c>bool</c>, <c>list</c>, <c>int</c>.</summary>
public enum RulesetParameterType
{
    Choice,
    Flag,
    List,
    Number,
}

/// <summary>The template's rules, laid out as docs/domain/rulesets.md's snapshot.</summary>
public sealed record RulesetValuesResponse(
    RulesetContestResponse Contest,
    RulesetLineupResponse Lineup,
    RulesetSubstitutionsResponse Substitutions,
    RulesetRosterResponse Roster,
    RulesetStandingsResponse Standings,
    RulesetOperationsResponse Operations,
    RulesetEligibilityResponse Eligibility)
{
    public static RulesetValuesResponse From(RulesetValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var (contest, lineup, substitutions, roster, standings, operations, eligibility) =
            (values.Contest, values.Lineup, values.Substitutions, values.Roster, values.Standings, values.Operations, values.Eligibility);
        var points = standings.Points;
        return new RulesetValuesResponse(
            new RulesetContestResponse(contest.PeriodSeconds, contest.TieResolution, contest.ExtraPeriodSequence, contest.CoverageDefault),
            new RulesetLineupResponse(
                lineup.Starters,
                lineup.BenchMax,
                lineup.RequiresApproval,
                lineup.Positions.ToDictionary(position => position.Key, position => new CountRangeResponse(position.Value.Min, position.Value.Max), StringComparer.Ordinal)),
            new RulesetSubstitutionsResponse(substitutions.Max, substitutions.Windows, substitutions.ExtraInExtraTime),
            new RulesetRosterResponse(roster.Min, roster.Max, roster.RequiresApproval, roster.LockPolicy),
            new RulesetStandingsResponse(
                new StandingsPointsResponse(points.Win, points.Draw, points.Loss, points.NoResult, points.ForfeitWin, points.ForfeitLoss),
                new ForfeitScoreResponse(standings.ForfeitScore.Scored, standings.ForfeitScore.Conceded),
                standings.Tiebreakers,
                standings.HeadToHeadReapply),
            new RulesetOperationsResponse(operations.Readiness, operations.OfficialsMinimum, operations.ScorerCorrectionWindowMinutes, operations.StartingSoonMinutes),
            new RulesetEligibilityResponse(eligibility.MembershipRequired, eligibility.MinAge, eligibility.MaxAge, eligibility.AgeReferenceDate, eligibility.MinorConsentRequired));
    }
}

/// <summary><c>periodSeconds</c> is keyed by the definition's period codes.</summary>
public sealed record RulesetContestResponse(
    IReadOnlyDictionary<string, int> PeriodSeconds,
    string TieResolution,
    IReadOnlyList<string> ExtraPeriodSequence,
    CoverageLevel CoverageDefault);

/// <summary><c>positions</c> is keyed by a position code or group of the definition.</summary>
public sealed record RulesetLineupResponse(int Starters, int BenchMax, bool RequiresApproval, IReadOnlyDictionary<string, CountRangeResponse> Positions);

public sealed record CountRangeResponse(int Min, int Max);

public sealed record RulesetSubstitutionsResponse(int Max, int Windows, int ExtraInExtraTime);

public sealed record RulesetRosterResponse(int Min, int Max, bool RequiresApproval, RosterLockPolicy LockPolicy);

/// <summary><c>tiebreakers</c> in the order they apply; <c>headToHeadReapply</c> is the file's <c>h2h_reapply</c>.</summary>
public sealed record RulesetStandingsResponse(
    StandingsPointsResponse Points,
    ForfeitScoreResponse ForfeitScore,
    IReadOnlyList<string> Tiebreakers,
    bool HeadToHeadReapply);

public sealed record StandingsPointsResponse(int Win, int Draw, int Loss, int NoResult, int ForfeitWin, int ForfeitLoss);

/// <summary>The score recorded for a forfeit (the file's <c>for</c> and <c>against</c>).</summary>
public sealed record ForfeitScoreResponse(int Scored, int Conceded);

/// <summary><c>officialsMinimum</c> is keyed by the definition's official role codes.</summary>
public sealed record RulesetOperationsResponse(
    IReadOnlyList<ReadinessCheck> Readiness,
    IReadOnlyDictionary<string, int> OfficialsMinimum,
    int ScorerCorrectionWindowMinutes,
    int StartingSoonMinutes);

/// <summary>Ages in whole years on <c>ageReferenceDate</c>; null means no limit.</summary>
public sealed record RulesetEligibilityResponse(
    bool MembershipRequired,
    int? MinAge,
    int? MaxAge,
    DateOnly? AgeReferenceDate,
    MinorConsentRequirement MinorConsentRequired);
