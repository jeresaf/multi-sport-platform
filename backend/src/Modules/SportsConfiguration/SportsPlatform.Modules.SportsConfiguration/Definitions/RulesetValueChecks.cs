using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The rules a template's values must keep against its sport definition and the tiebreaker catalogue, and between
/// themselves: periods, positions, official roles and coverage levels the definition has; a tie resolution it supports,
/// with an extra-period sequence that fits it; known tiebreakers that apply to the sport; ranges whose ends are in order;
/// lineups that fit the roster. The template validator runs them on a file's values; the resolver (S05-T4b) on the
/// values after a tenant's changes. Each problem names the rule's dotted path.
/// </summary>
public static class RulesetValueChecks
{
    /// <summary>The path of the standings' tiebreakers (a list parameter whose values are tiebreaker codes).</summary>
    public const string StandingsTiebreakersPath = "standings.tiebreakers";

    /// <summary>The path of the contest's tie resolution.</summary>
    public const string TieResolutionPath = "contest.tie_resolution";

    public static IReadOnlyList<RulesetValueProblem> Check(RulesetValues values, SportDefinition sport, Func<string, TiebreakerDefinition?> tiebreakers)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(sport);
        ArgumentNullException.ThrowIfNull(tiebreakers);
        List<RulesetValueProblem> problems = [];
        Contest(values.Contest, sport, problems);
        Lineup(values, sport, problems);
        Officials(values.Operations, sport, problems);
        Tiebreakers(values.Standings, sport, tiebreakers, problems);
        if (values.Eligibility is { MinAge: { } min, MaxAge: { } max } && max < min)
        {
            problems.Add(new("eligibility.max_age", RulesetValueProblemKind.Range, $"{max} is below min_age ({min})"));
        }

        return problems;
    }

    /// <summary>An enum value as the files write it (<c>STANDARD</c>).</summary>
    internal static string Upper<TEnum>(TEnum value)
        where TEnum : struct, Enum => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    private static void Contest(RulesetContest contest, SportDefinition sport, List<RulesetValueProblem> problems)
    {
        var periods = sport.Periods.ToDictionary(period => period.Code, StringComparer.Ordinal);
        foreach (var code in contest.PeriodSeconds.Keys.Where(code => !periods.ContainsKey(code)))
        {
            problems.Add(new($"contest.period_seconds.{code}", RulesetValueProblemKind.Reference, $"unknown period \"{code}\""));
        }

        if (!sport.CoverageLevels.Contains(contest.CoverageDefault))
        {
            problems.Add(new("contest.coverage_default", RulesetValueProblemKind.Reference, $"the sport has no {Upper(contest.CoverageDefault)} coverage"));
        }

        var resolution = contest.TieResolution;
        if (!sport.Supports.TieResolutions.Contains(resolution, StringComparer.Ordinal))
        {
            problems.Add(new(TieResolutionPath, RulesetValueProblemKind.TieResolution, $"the sport doesn't support \"{resolution}\""));
            return;
        }

        var sequence = contest.ExtraPeriodSequence;
        for (var index = 0; index < sequence.Count; index++)
        {
            if (!periods.ContainsKey(sequence[index]))
            {
                problems.Add(new("contest.extra_period_sequence", RulesetValueProblemKind.Reference, $"unknown period \"{sequence[index]}\" at {index}"));
            }
        }

        if (resolution == TieResolutions.Draw && sequence.Count > 0)
        {
            problems.Add(new("contest.extra_period_sequence", RulesetValueProblemKind.TieResolution, $"tie resolution {resolution} plays no extra periods"));
        }
        else if (resolution != TieResolutions.Draw && sequence.Count == 0)
        {
            problems.Add(new("contest.extra_period_sequence", RulesetValueProblemKind.TieResolution, $"tie resolution {resolution} needs its extra periods"));
        }
        else if (resolution is TieResolutions.Shootout or TieResolutions.ExtraPeriodsThenShootout
            && !sequence.Any(code => periods.TryGetValue(code, out var period) && period.Kind == PhaseKind.Shootout))
        {
            problems.Add(new("contest.extra_period_sequence", RulesetValueProblemKind.TieResolution, $"tie resolution {resolution} needs a shoot-out period"));
        }
    }

    private static void Lineup(RulesetValues values, SportDefinition sport, List<RulesetValueProblem> problems)
    {
        var (lineup, roster) = (values.Lineup, values.Roster);
        var positions = sport.Positions.SelectMany(position => new[] { position.Code, position.Group }).ToHashSet(StringComparer.Ordinal);
        foreach (var (code, range) in lineup.Positions)
        {
            var path = $"lineup.positions.{code}";
            if (!positions.Contains(code))
            {
                problems.Add(new(path, RulesetValueProblemKind.Reference, $"unknown position or position group \"{code}\""));
            }

            if (range.Max < range.Min)
            {
                problems.Add(new($"{path}.max", RulesetValueProblemKind.Range, $"{range.Max} is below min ({range.Min})"));
            }
            else if (range.Min > lineup.Starters)
            {
                problems.Add(new($"{path}.min", RulesetValueProblemKind.Range, $"{range.Min} is more than the {lineup.Starters} starters"));
            }
        }

        if (roster.Max < roster.Min)
        {
            problems.Add(new("roster.max", RulesetValueProblemKind.Range, $"{roster.Max} is below roster.min ({roster.Min})"));
        }

        if (lineup.Starters > roster.Min)
        {
            problems.Add(new("roster.min", RulesetValueProblemKind.Range, $"{roster.Min} is fewer than the {lineup.Starters} starters"));
        }

        if (lineup.Starters + lineup.BenchMax > roster.Max)
        {
            problems.Add(new("roster.max", RulesetValueProblemKind.Range,
                $"{roster.Max} is fewer than the {lineup.Starters} starters and {lineup.BenchMax} on the bench"));
        }
    }

    private static void Officials(RulesetOperations operations, SportDefinition sport, List<RulesetValueProblem> problems)
    {
        var roles = sport.OfficialRoles.Select(role => role.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var code in operations.OfficialsMinimum.Keys.Where(code => !roles.Contains(code)))
        {
            problems.Add(new($"operations.officials_minimum.{code}", RulesetValueProblemKind.Reference, $"unknown official role \"{code}\""));
        }
    }

    private static void Tiebreakers(
        RulesetStandings standings, SportDefinition sport, Func<string, TiebreakerDefinition?> catalogue, List<RulesetValueProblem> problems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in standings.Tiebreakers)
        {
            var problem = catalogue(code) switch
            {
                null => $"unknown tiebreaker \"{code}\"",
                { } tiebreaker when !tiebreaker.AppliesTo(sport.Sport) => $"tiebreaker \"{code}\" doesn't apply to {sport.Name}",
                _ when !seen.Add(code) => $"tiebreaker \"{code}\" is listed twice",
                _ => null,
            };
            if (problem is not null)
            {
                problems.Add(new(StandingsTiebreakersPath, RulesetValueProblemKind.Tiebreaker, problem));
            }
        }
    }
}

/// <summary>A broken rule: the dotted path it names, its kind (which error the resolver answers) and what is wrong.</summary>
public sealed record RulesetValueProblem(string Path, RulesetValueProblemKind Kind, string Message);

public enum RulesetValueProblemKind
{
    /// <summary>A code the sport definition doesn't have (a period, a position, an official role, a coverage level).</summary>
    Reference,

    /// <summary>A value outside its range, or two values out of order.</summary>
    Range,

    /// <summary>A tie resolution the sport doesn't support, or extra periods that don't fit it.</summary>
    TieResolution,

    /// <summary>A tiebreaker not in the catalogue, not for this sport, or listed twice.</summary>
    Tiebreaker,
}
