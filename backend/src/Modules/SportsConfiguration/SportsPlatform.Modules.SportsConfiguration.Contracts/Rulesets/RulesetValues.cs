using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// A template's rules as docs/domain/rulesets.md's snapshot example lays them out: what a contest plays by, lineups,
/// substitutions, rosters, standings, match-day operations and eligibility. Members follow the file's keys. Later sports
/// add members with a schema change; a published template never changes.
/// </summary>
public sealed record RulesetValues(
    RulesetContest Contest,
    RulesetLineup Lineup,
    RulesetSubstitutions Substitutions,
    RulesetRoster Roster,
    RulesetStandings Standings,
    RulesetOperations Operations,
    RulesetEligibility Eligibility);

/// <summary>
/// <see cref="PeriodSeconds"/> is keyed by the definition's period codes; <see cref="TieResolution"/> is one the sport
/// supports, with the overtime periods it plays in <see cref="ExtraPeriodSequence"/> (empty for <c>DRAW</c>).
/// </summary>
public sealed record RulesetContest(
    IReadOnlyDictionary<string, int> PeriodSeconds,
    string TieResolution,
    IReadOnlyList<string> ExtraPeriodSequence,
    CoverageLevel CoverageDefault);

/// <summary><see cref="Positions"/> is keyed by a position code or group of the definition.</summary>
public sealed record RulesetLineup(
    int Starters,
    int BenchMax,
    bool RequiresApproval,
    IReadOnlyDictionary<string, CountRange> Positions);

/// <summary>Both ends included.</summary>
public sealed record CountRange(int Min, int Max);

public sealed record RulesetSubstitutions(int Max, int Windows, int ExtraInExtraTime);

public sealed record RulesetRoster(int Min, int Max, bool RequiresApproval, RosterLockPolicy LockPolicy);

/// <summary>When a roster locks; Competitions (S07/S09) adds others with a schema change.</summary>
public enum RosterLockPolicy
{
    /// <summary>At the participant's first contest.</summary>
    FirstContest,
}

/// <summary><see cref="Tiebreakers"/> in the order they apply, each a code of the tiebreaker catalogue.</summary>
public sealed record RulesetStandings(
    StandingsPoints Points,
    ForfeitScore ForfeitScore,
    IReadOnlyList<string> Tiebreakers,
    [property: JsonPropertyName("h2h_reapply")] bool HeadToHeadReapply);

public sealed record StandingsPoints(int Win, int Draw, int Loss, int NoResult, int ForfeitWin, int ForfeitLoss);

/// <summary>The score recorded for a forfeit: the file's <c>for</c> and <c>against</c>.</summary>
public sealed record ForfeitScore(
    [property: JsonPropertyName("for")] int Scored,
    [property: JsonPropertyName("against")] int Conceded);

/// <summary><see cref="OfficialsMinimum"/> is keyed by the definition's official role codes.</summary>
public sealed record RulesetOperations(
    IReadOnlyList<ReadinessCheck> Readiness,
    IReadOnlyDictionary<string, int> OfficialsMinimum,
    int ScorerCorrectionWindowMinutes,
    int StartingSoonMinutes);

/// <summary>What must hold before a contest may start (S11 builds the checks; it adds others with a schema change).</summary>
public enum ReadinessCheck
{
    LineupsApproved,
    OfficialsMinimum,
    VenueSet,
}

/// <summary>Ages are in whole years on <see cref="AgeReferenceDate"/>; null means no limit (or no date).</summary>
public sealed record RulesetEligibility(
    bool MembershipRequired,
    int? MinAge,
    int? MaxAge,
    DateOnly? AgeReferenceDate,
    MinorConsentRequirement MinorConsentRequired);

/// <summary>Whether a minor needs a guardian's consent to take part; the registry slices add the others.</summary>
public enum MinorConsentRequirement
{
    /// <summary>As the tenant's settings say (<c>minor_registration_consent</c>).</summary>
    TenantDefault,
}
