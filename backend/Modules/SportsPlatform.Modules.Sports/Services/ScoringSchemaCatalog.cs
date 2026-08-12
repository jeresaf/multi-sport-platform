using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Modules.Sports.Services;

public sealed record ScoringSchema(
    string SportCode,
    string Name,
    string PrimaryMetric,
    string ScoreLabel,
    bool LowerIsBetter,
    bool SupportsClock,
    bool ClockCountsDown,
    IReadOnlyList<ScoringActionSchema> Actions,
    IReadOnlyList<ScoringSegmentSchema> Segments,
    IReadOnlyList<EventDefinition>? EventDefinitions = null);

public sealed record ScoringActionSchema(
    string Code,
    string Label,
    string Metric,
    decimal DefaultValue,
    string InputType = "fixed",
    decimal? Minimum = null,
    decimal? Maximum = null,
    decimal? Step = null,
    string? Unit = null,
    string Tone = "primary",
    bool RequiresParticipant = true,
    bool RequiresSegment = false,
    bool RequiresPlayer = false,
    string? EventType = null);

public sealed record ScoringSegmentSchema(
    Guid Id,
    string Name,
    string Type,
    int SortOrder,
    string Status,
    Guid? ParentSegmentId = null,
    int Depth = 0,
    bool IsOptional = false,
    string? ActivationCondition = null);

public static class ScoringSchemaCatalog
{
    private static readonly Guid Football =
        Guid.Parse("00000000-0000-0000-0000-000000000100");
    private static readonly Guid Basketball =
        Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid Golf =
        Guid.Parse("00000000-0000-0000-0000-000000000102");
    private static readonly Guid Athletics =
        Guid.Parse("00000000-0000-0000-0000-000000000103");
    private static readonly Guid Cricket =
        Guid.Parse("00000000-0000-0000-0000-000000000104");
    private static readonly Guid Combat =
        Guid.Parse("00000000-0000-0000-0000-000000000105");
    private static readonly Guid Rugby =
        Guid.Parse("00000000-0000-0000-0000-000000000106");

    public static ScoringSchema? ForSport(
        Guid sportId,
        IReadOnlyList<ScoringSegmentSchema> segments)
    {
        if (sportId == Football)
        {
            return Schema("football", "Football", "score", "goals", false, true, false, segments,
                Fixed("goal", "Goal", "score"),
                Fixed("own-goal", "Own goal for team", "score"),
                Fixed("penalty-goal", "Penalty goal", "score"),
                Fixed("penalty-scored", "Shootout penalty scored", "shootout_score"),
                Fixed("penalty-missed", "Shootout penalty missed", "shootout_misses", tone: "danger"),
                Fixed("yellow-card", "Yellow card", "yellow_cards", tone: "secondary"),
                Fixed("second-yellow", "Second yellow", "red_cards", tone: "danger"),
                Fixed("red-card", "Red card", "red_cards", tone: "danger"),
                Fixed("substitution-in", "Substitution", "substitutions", tone: "secondary"));
        }

        if (sportId == Basketball)
        {
            return Schema("basketball", "Basketball", "score", "points", false, true, true, segments,
                Fixed("free-throw", "+1 Free throw", "score", eventType: "FREE_THROW_MADE", requiresPlayer: true),
                Fixed("field-goal", "+2 Field goal", "score", 2, eventType: "FIELD_GOAL_MADE", requiresPlayer: true),
                Fixed("three-pointer", "+3 Three-pointer", "score", 3, eventType: "THREE_POINTER_MADE", requiresPlayer: true),
                Fixed("team-foul", "Team foul", "team_fouls", tone: "danger", eventType: "TEAM_FOUL"),
                Fixed("personal-foul", "Personal foul", "personal_fouls", tone: "danger", eventType: "PERSONAL_FOUL", requiresPlayer: true),
                Fixed("timeout", "Timeout", "timeouts", tone: "secondary", eventType: "TIMEOUT"));
        }

        if (sportId == Golf)
        {
            return Schema("golf", "Golf", "strokes", "strokes", true, false, false, segments,
                Number("hole-strokes", "Record hole strokes", "strokes", 4, 1, 20, 1, "strokes", true),
                Fixed("penalty", "Penalty stroke", "penalties", tone: "danger"));
        }

        if (sportId == Athletics)
        {
            return Schema("athletics", "Athletics", "time_seconds", "seconds", true, true, false, segments,
                Number("time", "Record time", "time_seconds", 10, 0, 86400, 0.001m, "seconds"),
                Number("distance", "Record distance", "distance_metres", 1, 0, 1000, 0.001m, "metres"),
                Fixed("qualification", "Qualified", "qualifications"),
                Fixed("disqualification", "Disqualified", "disqualifications", tone: "danger"));
        }

        if (sportId == Cricket)
        {
            return Schema("cricket", "Cricket", "score", "runs", false, false, false, segments,
                Fixed("single", "+1 run", "score"),
                Fixed("four", "Four", "score", 4),
                Fixed("six", "Six", "score", 6),
                Fixed("wicket", "Wicket", "wickets", tone: "danger"),
                Fixed("extra", "Extra", "extras", tone: "secondary"));
        }

        if (sportId == Combat)
        {
            return Schema("combat", "Combat", "score", "points", false, true, true, segments,
                Number("judge-score", "Judge score", "score", 10, 0, 10, 1, "points", true),
                Fixed("significant-strike", "Significant strike", "significant_strikes"),
                Fixed("takedown", "Takedown", "takedowns"),
                Fixed("knockdown", "Knockdown", "knockdowns", tone: "danger"),
                Fixed("stoppage", "Stoppage", "stoppages", tone: "danger"));
        }

        if (sportId == Rugby)
        {
            return Schema("rugby", "Rugby Union", "score", "points", false, true, false, segments,
                Fixed("try", "Try", "score", 5),
                Fixed("conversion", "Conversion", "score", 2),
                Fixed("penalty-try", "Penalty try", "score", 7),
                Fixed("penalty-goal", "Penalty goal", "score", 3),
                Fixed("drop-goal", "Drop goal", "score", 3),
                Fixed("yellow-card", "Yellow card", "yellow_cards", tone: "secondary"),
                Fixed("red-card", "Red card", "red_cards", tone: "danger"),
                Fixed("substitution", "Substitution", "substitutions", tone: "secondary"));
        }

        return null;
    }

    private static ScoringSchema Schema(
        string code,
        string name,
        string primaryMetric,
        string scoreLabel,
        bool lowerIsBetter,
        bool supportsClock,
        bool clockCountsDown,
        IReadOnlyList<ScoringSegmentSchema> segments,
        params ScoringActionSchema[] actions) =>
        new(
            code,
            name,
            primaryMetric,
            scoreLabel,
            lowerIsBetter,
            supportsClock,
            clockCountsDown,
            actions.Select(action => action with
            {
                RequiresSegment = segments.Count > 0
            }).ToArray(),
            segments);

    private static ScoringActionSchema Fixed(
        string code,
        string label,
        string metric,
        decimal value = 1,
        string tone = "primary",
        bool requiresPlayer = false,
        string? eventType = null) =>
        new(code, label, metric, value, Tone: tone,
            RequiresPlayer: requiresPlayer, EventType: eventType);

    private static ScoringActionSchema Number(
        string code,
        string label,
        string metric,
        decimal value,
        decimal minimum,
        decimal maximum,
        decimal step,
        string unit,
        bool requiresSegment = false) =>
        new(code, label, metric, value, "number", minimum, maximum, step, unit,
            RequiresSegment: requiresSegment);
}
