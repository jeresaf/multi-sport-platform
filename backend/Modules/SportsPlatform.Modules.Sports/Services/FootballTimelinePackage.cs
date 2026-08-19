using System.Text.Json;

namespace SportsPlatform.Modules.Sports.Services;

public sealed record FootballTimelineEventSpec(
    string Id,
    string Name,
    string Category,
    string Importance,
    string? RequiredMetric,
    decimal DefaultValue,
    bool RequiresParticipant,
    bool RequiresPrimaryPlayer,
    bool RequiresSecondaryPlayer,
    IReadOnlyList<string> SegmentApplicability,
    string TimingRequirement,
    string CorrectionPolicy,
    string ScoreEffect,
    object Validation)
{
    // The current scoring contract uses its secondary participant slot for a
    // roster player. Keep that compatibility flag while the richer actor
    // fields are introduced.
    public bool RequiresSecondaryParticipant => RequiresPrimaryPlayer;
}

public static class FootballTimelinePackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<FootballTimelineEventSpec> Events { get; } =
    [
        System("MATCH_START", "Match Start", "SYSTEM", "KEY"),
        System("PERIOD_START", "Period Start", "PERIOD", "KEY", ["Half"]),
        System("PERIOD_END", "Period End", "PERIOD", "KEY", ["Half"]),
        Team("GOAL", "Goal", "SCORING", "KEY", "score", 1, "REGULATION", ["Half", "Overtime"], "optional"),
        Team("OWN_GOAL", "Own Goal", "SCORING", "KEY", "score", 1, "REGULATION", ["Half", "Overtime"], "optional"),
        Team("PENALTY_GOAL", "Penalty Goal", "SCORING", "KEY", "score", 1, "REGULATION", ["Half", "Overtime"], "optional"),
        Team("PENALTY_MISSED", "Penalty Missed", "SCORING", "KEY", "penalty_misses", 1, "NONE", player: "optional"),
        Team("ASSIST", "Assist", "PLAY", "STANDARD", "assists", 1, "NONE", player: "required"),
        Team("YELLOW_CARD", "Yellow Card", "DISCIPLINE", "KEY", "yellow_cards", 1, "NONE", player: "optional"),
        Team("SECOND_YELLOW_CARD", "Second Yellow Card", "DISCIPLINE", "KEY", "red_cards", 1, "NONE", player: "optional"),
        Team("RED_CARD", "Red Card", "DISCIPLINE", "KEY", "red_cards", 1, "NONE", player: "optional"),
        Team("SUBSTITUTION", "Substitution", "PERSONNEL", "KEY", "substitutions", 1, "NONE", player: "required", secondaryPlayer: true),
        Team("PLAYER_IN", "Player In", "PERSONNEL", "STANDARD", "player_in", 1, "NONE", player: "required"),
        Team("PLAYER_OUT", "Player Out", "PERSONNEL", "STANDARD", "player_out", 1, "NONE", player: "required"),
        System("VAR_REVIEW", "VAR Review", "REVIEW", "KEY", requiredMetric: "reviews", defaultValue: 1),
        System("VAR_DECISION", "VAR Decision", "REVIEW", "KEY", requiredMetric: "reviews", defaultValue: 1),
        Team("INJURY", "Injury", "STOPPAGE", "KEY", "injuries", 1, "NONE", player: "optional"),
        System("INJURY_TIME", "Injury Time", "STOPPAGE", "KEY", requiredMetric: "injury_time_minutes", defaultValue: 1),
        System("PENALTY_SHOOTOUT_START", "Penalty Shootout Start", "PERIOD", "KEY", ["Shootout"]),
        Team("PENALTY_SHOOTOUT_SCORE", "Shootout Penalty Scored", "SCORING", "KEY", "shootout_score", 1, "SHOOTOUT", ["Shootout"], "optional"),
        Team("PENALTY_SHOOTOUT_MISS", "Shootout Penalty Missed", "SCORING", "KEY", "shootout_misses", 1, "NONE", ["Shootout"], "optional"),
        System("MATCH_END", "Match End", "SYSTEM", "KEY"),
        Team("SHOT", "Shot", "PLAY", "DETAILED", "shots", 1, "NONE", player: "optional"),
        Team("SHOT_ON_TARGET", "Shot on Target", "PLAY", "DETAILED", "shots_on_target", 1, "NONE", player: "optional"),
        Team("SHOT_OFF_TARGET", "Shot Off Target", "PLAY", "DETAILED", "shots_off_target", 1, "NONE", player: "optional"),
        Team("BLOCKED_SHOT", "Blocked Shot", "PLAY", "DETAILED", "blocked_shots", 1, "NONE", player: "optional"),
        Team("CORNER", "Corner", "PLAY", "DETAILED", "corners", 1, "NONE"),
        Team("OFFSIDE", "Offside", "PLAY", "DETAILED", "offsides", 1, "NONE", player: "optional"),
        Team("FOUL", "Foul", "DISCIPLINE", "DETAILED", "fouls", 1, "NONE", player: "optional"),
        Team("FREE_KICK", "Free Kick", "PLAY", "DETAILED", "free_kicks", 1, "NONE")
    ];

    public static FootballTimelineEventSpec? Find(string eventType) =>
        Events.Concat(Aliases)
            .FirstOrDefault(item => item.Id.Equals(eventType, StringComparison.OrdinalIgnoreCase));

    public static string GetDefinitionsJson() =>
        JsonSerializer.Serialize(Events.Concat(Aliases), JsonOptions);

    private static IReadOnlyList<FootballTimelineEventSpec> Aliases { get; } =
    [
        Team("SECOND_YELLOW", "Second Yellow Card", "DISCIPLINE", "KEY", "red_cards", 1, "NONE", player: "optional"),
        Team("SUBSTITUTION_IN", "Player In", "PERSONNEL", "STANDARD", "player_in", 1, "NONE", player: "required"),
        Team("SUBSTITUTION_OUT", "Player Out", "PERSONNEL", "STANDARD", "player_out", 1, "NONE", player: "required"),
        Team("PENALTY_SCORED", "Shootout Penalty Scored", "SCORING", "KEY", "shootout_score", 1, "SHOOTOUT", ["Shootout"], "optional"),
        Team("PENALTY_SAVED", "Shootout Penalty Saved", "SCORING", "KEY", "shootout_misses", 1, "NONE", ["Shootout"], "optional")
    ];

    private static FootballTimelineEventSpec System(
        string id,
        string name,
        string category,
        string importance,
        IReadOnlyList<string>? segments = null,
        string? requiredMetric = null,
        decimal defaultValue = 0) =>
        new(id, name, category, importance, requiredMetric, defaultValue,
            false, false, false, segments ?? ["*"], "TimestampOrClock",
            "ApprovalRequired", "NONE", ValueRules(defaultValue));

    private static FootballTimelineEventSpec Team(
        string id,
        string name,
        string category,
        string importance,
        string metric,
        decimal value,
        string scoreEffect,
        IReadOnlyList<string>? segments = null,
        string player = "none",
        bool secondaryPlayer = false) =>
        new(id, name, category, importance, metric, value,
            true, player == "required", secondaryPlayer,
            segments ?? ["Half"], "TimestampOrClock", "ApprovalRequired",
            scoreEffect, ValueRules(value));

    private static object ValueRules(decimal value) => new
    {
        minimumValue = value,
        maximumValue = value,
        minimumClockSeconds = 0,
        maximumClockSeconds = 7_200
    };
}
