namespace SportsPlatform.UnitTests.Engines;

/// <summary>
/// The rules of sport-engine.md §5 (with the §4 checks and the corrections of §9) that scenarios show, by the code a
/// scenario's <c>covers</c> names. Each needs a scenario that records an event of the sport under it and, unless it
/// can't refuse anything, one that refuses an event with one of the rule's codes (§11).
/// </summary>
internal static class ScenarioRules
{
    public static IReadOnlyList<ScenarioRule> All { get; } =
    [
        new("APPLY", "§5 Apply: an event's points go to its participant or the opponent, in its component")
        {
            AcceptOnly = "Apply runs on recorded events; refusing them is the Validate rules' job.",
        },
        new("APPLY_POINTS_OVERRIDE", "§5 Apply: the rules may change what a scoring event type is worth")
        {
            AcceptOnly = "An override changes the points of recorded events; it refuses nothing.",
        },
        new("VALIDATE_EVENT_TYPE", "§4: the event type is the definition's or a generic one") { Codes = ["TIMELINE_EVENT_UNKNOWN"] },
        new("VALIDATE_STATUS", "§5 Validate 1: the contest is live, or ended within the correction window, or the source is BACKFILL or ADMIN") { Codes = ["TIMELINE_CONTEST_NOT_LIVE"] },
        new("VALIDATE_PHASE", "§5 Validate 2: the event type is allowed in the current phase kind") { Codes = ["TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE"] },
        new("VALIDATE_PARTICIPANT", "§4: an event of a side names one of the contest's participants") { Codes = ["TIMELINE_PARTICIPANT_INVALID"] },
        new("VALIDATE_ACTORS", "§5 Validate 3: required actors are named and, from STANDARD, where their role draws from") { Codes = ["TIMELINE_ACTOR_REQUIRED", "TIMELINE_ACTOR_NOT_ON_FIELD", "TIMELINE_ACTOR_INVALID"] },
        new("VALIDATE_FOLLOW_UPS", "§5 Validate 4: a follow-up has the earlier event it follows") { Codes = ["TIMELINE_FOLLOW_UP_MISSING"] },
        new("VALIDATE_LIMITS", "§5 Validate 4: the rules' limits are kept") { Codes = ["TIMELINE_LIMIT_REACHED"] },
        new("OUTCOME_WINNER", "§5 Outcome: different main totals win, in regulation or in extra periods")
        {
            AcceptOnly = "The outcome is proposed from recorded events; it refuses nothing.",
        },
        new("OUTCOME_DRAW", "§5 Outcome: level totals are a draw when the tie resolution is DRAW")
        {
            AcceptOnly = "The outcome is proposed from recorded events; it refuses nothing.",
        },
        new("OUTCOME_EXTRA_PERIODS", "§5 Outcome: level totals require the next extra period of contest.extra_period_sequence")
        {
            AcceptOnly = "The outcome is proposed from recorded events; which period starts next is Contest Operations' offer (S12).",
        },
        new("OUTCOME_SHOOTOUT", "§5 Outcome: level after the extra periods requires the shoot-out, won on its own component")
        {
            AcceptOnly = "The outcome is proposed from recorded events; shoot-out events outside the shoot-out are VALIDATE_PHASE's.",
        },
        new("OUTCOME_SUDDEN_DEATH", "§5 Outcome: the first score in a sudden_death period wins and ends the contest; no other score follows it") { Codes = ["TIMELINE_EVENT_NOT_ALLOWED_IN_PHASE"] },
        new("CORRECTIONS_AND_REVERSALS", "§9: a correction or a reversal names an event that still stands, in a way its type allows") { Codes = ["TIMELINE_REFERENCE_INVALID"] },
    ];
}

/// <summary>A rule, the error codes it refuses with and, when it can't refuse an event, why it has no rejecting scenario.</summary>
internal sealed record ScenarioRule(string Code, string Description)
{
    public IReadOnlyList<string> Codes { get; init; } = [];

    public string? AcceptOnly { get; init; }
}
