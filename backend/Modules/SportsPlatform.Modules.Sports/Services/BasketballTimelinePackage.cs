using System.Text.Json;

namespace SportsPlatform.Modules.Sports.Services;

public sealed record BasketballTimelineEventSpec(
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
    public bool RequiresSecondaryParticipant => RequiresPrimaryPlayer;
    public decimal Points => ScoreEffect == "REGULATION" ? DefaultValue : 0;
    public bool RequiresMinute => Category is not ("SYSTEM" or "PERIOD");
    public bool RequiresPlayer => RequiresPrimaryPlayer;
    public bool ResetsEachPeriod => Id == "TEAM_FOUL";
}

public static class BasketballTimelinePackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<BasketballTimelineEventSpec> Events { get; } =
    [
        System("GAME_START", "Game Start", "SYSTEM", "KEY"),
        System("PERIOD_START", "Period Start", "PERIOD", "KEY", ["Quarter", "Overtime"]),
        System("PERIOD_END", "Period End", "PERIOD", "KEY", ["Quarter", "Overtime"]),
        Team("FREE_THROW_MADE", "Free Throw Made", "SCORING", "KEY", "score", 1, "REGULATION", player: "required"),
        Team("FREE_THROW_MISSED", "Free Throw Missed", "PLAY", "STANDARD", "free_throw_misses", 1, "NONE", player: "required"),
        Team("TWO_POINT_MADE", "Two Point Shot Made", "SCORING", "KEY", "score", 2, "REGULATION", player: "required"),
        Team("TWO_POINT_MISSED", "Two Point Shot Missed", "PLAY", "STANDARD", "two_point_misses", 1, "NONE", player: "required"),
        Team("THREE_POINT_MADE", "Three Point Shot Made", "SCORING", "KEY", "score", 3, "REGULATION", player: "required"),
        Team("THREE_POINT_MISSED", "Three Point Shot Missed", "PLAY", "STANDARD", "three_point_misses", 1, "NONE", player: "required"),
        Team("ASSIST", "Assist", "PLAY", "STANDARD", "assists", 1, "NONE", player: "required"),
        Team("REBOUND_OFFENSIVE", "Offensive Rebound", "PLAY", "STANDARD", "offensive_rebounds", 1, "NONE", player: "required"),
        Team("REBOUND_DEFENSIVE", "Defensive Rebound", "PLAY", "STANDARD", "defensive_rebounds", 1, "NONE", player: "required"),
        Team("STEAL", "Steal", "PLAY", "STANDARD", "steals", 1, "NONE", player: "required"),
        Team("BLOCK", "Block", "PLAY", "STANDARD", "blocks", 1, "NONE", player: "required"),
        Team("TURNOVER", "Turnover", "PLAY", "STANDARD", "turnovers", 1, "NONE", player: "optional"),
        Team("FOUL_PERSONAL", "Personal Foul", "DISCIPLINE", "STANDARD", "personal_fouls", 1, "NONE", player: "required"),
        Team("FOUL_TECHNICAL", "Technical Foul", "DISCIPLINE", "KEY", "technical_fouls", 1, "NONE", player: "optional"),
        Team("FOUL_UNSPORTSMANLIKE", "Unsportsmanlike Foul", "DISCIPLINE", "KEY", "unsportsmanlike_fouls", 1, "NONE", player: "optional"),
        Team("FOUL_DISQUALIFYING", "Disqualifying Foul", "DISCIPLINE", "KEY", "disqualifying_fouls", 1, "NONE", player: "required"),
        Team("SUBSTITUTION", "Substitution", "PERSONNEL", "STANDARD", "substitutions", 1, "NONE", player: "required", secondaryPlayer: true),
        Team("TIMEOUT", "Timeout", "STOPPAGE", "KEY", "timeouts", 1, "NONE"),
        Team("JUMP_BALL", "Jump Ball", "PLAY", "STANDARD", "jump_balls", 1, "NONE"),
        System("GAME_END", "Game End", "SYSTEM", "KEY")
    ];

    public static BasketballTimelineEventSpec? Find(string eventType) =>
        Events.Concat(Aliases).FirstOrDefault(item =>
            item.Id.Equals(eventType, StringComparison.OrdinalIgnoreCase));

    public static string CanonicalType(string eventType) => Normalize(eventType) switch
    {
        "FIELD_GOAL_MADE" => "TWO_POINT_MADE",
        "FIELD_GOAL_MISSED" => "TWO_POINT_MISSED",
        "THREE_POINTER_MADE" => "THREE_POINT_MADE",
        "THREE_POINTER_MISSED" => "THREE_POINT_MISSED",
        "OFFENSIVE_REBOUND" => "REBOUND_OFFENSIVE",
        "DEFENSIVE_REBOUND" => "REBOUND_DEFENSIVE",
        "PERSONAL_FOUL" or "TEAM_FOUL" or "SHOOTING_FOUL" => "FOUL_PERSONAL",
        "TECHNICAL_FOUL" => "FOUL_TECHNICAL",
        "FLAGRANT_FOUL_1" => "FOUL_UNSPORTSMANLIKE",
        "FLAGRANT_FOUL_2" => "FOUL_DISQUALIFYING",
        "SUBSTITUTION_IN" or "SUBSTITUTION_OUT" => "SUBSTITUTION",
        _ => eventType
    };

    public static string GetDefinitionsJson() =>
        JsonSerializer.Serialize(Events.Concat(Aliases), JsonOptions);

    private static IReadOnlyList<BasketballTimelineEventSpec> Aliases { get; } =
    [
        Alias("FIELD_GOAL_MADE", "TWO_POINT_MADE"),
        Alias("FIELD_GOAL_MISSED", "TWO_POINT_MISSED"),
        Alias("THREE_POINTER_MADE", "THREE_POINT_MADE"),
        Alias("THREE_POINTER_MISSED", "THREE_POINT_MISSED"),
        Alias("OFFENSIVE_REBOUND", "REBOUND_OFFENSIVE"),
        Alias("DEFENSIVE_REBOUND", "REBOUND_DEFENSIVE"),
        Alias("PERSONAL_FOUL", "FOUL_PERSONAL"),
        Alias("TEAM_FOUL", "FOUL_PERSONAL"),
        Alias("SHOOTING_FOUL", "FOUL_PERSONAL"),
        Alias("TECHNICAL_FOUL", "FOUL_TECHNICAL"),
        Alias("FLAGRANT_FOUL_1", "FOUL_UNSPORTSMANLIKE"),
        Alias("FLAGRANT_FOUL_2", "FOUL_DISQUALIFYING"),
        Alias("SUBSTITUTION_IN", "SUBSTITUTION"),
        Alias("SUBSTITUTION_OUT", "SUBSTITUTION")
    ];

    private static BasketballTimelineEventSpec Alias(string id, string canonical) =>
        FindCanonical(canonical) with { Id = id };

    private static BasketballTimelineEventSpec FindCanonical(string id) =>
        Events.First(item => item.Id == id);

    private static BasketballTimelineEventSpec System(
        string id, string name, string category, string importance,
        IReadOnlyList<string>? segments = null) =>
        new(id, name, category, importance, null, 0, false, false, false,
            segments ?? ["*"], "TimestampOrClock", "ApprovalRequired", "NONE", ValueRules(0));

    private static BasketballTimelineEventSpec Team(
        string id, string name, string category, string importance,
        string metric, decimal value, string scoreEffect,
        string player = "none", bool secondaryPlayer = false) =>
        new(id, name, category, importance, metric, value, true,
            player == "required", secondaryPlayer, ["Quarter", "Overtime"],
            "TimestampOrClock", "ApprovalRequired", scoreEffect, ValueRules(value));

    private static object ValueRules(decimal value) => new
    {
        minimumValue = value,
        maximumValue = value,
        minimumClockSeconds = 0,
        maximumClockSeconds = 2_880
    };

    private static string Normalize(string value) => string.Join('_', value.Trim().ToUpperInvariant()
        .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries));
}
