using System.Text.Json;

namespace SportsPlatform.Modules.Sports.Services;

public sealed record RugbyTimelineEventSpec(
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
    public bool RequiresAssist => RequiresSecondaryPlayer;
    public bool IsPrimary => Category == "SCORING" && DefaultValue >= 5;
}

public static class RugbyTimelinePackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<RugbyTimelineEventSpec> Events(string variant = RugbyVariants.Fifteens)
    {
        variant = RugbyVariants.Normalize(variant);
        var maxMinute = variant == RugbyVariants.Sevens ? 30 : 100;
        var sinBinMinutes = variant == RugbyVariants.Sevens ? 2 : 10;
        var maxSubstitutions = variant == RugbyVariants.Sevens ? 5 : 8;
        return
        [
            System("MATCH_START", "Match Start", "SYSTEM", "KEY", maxMinute),
            System("PERIOD_START", "Period Start", "PERIOD", "KEY", maxMinute),
            System("PERIOD_END", "Period End", "PERIOD", "KEY", maxMinute),
            Team("TRY", "Try", "SCORING", "KEY", "score", 5, "REGULATION", maxMinute, player: "optional"),
            Team("PENALTY_TRY", "Penalty Try", "SCORING", "KEY", "score", 7, "REGULATION", maxMinute),
            Team("CONVERSION_MADE", "Conversion Made", "SCORING", "KEY", "score", 2, "REGULATION", maxMinute, player: "optional"),
            Team("CONVERSION_MISSED", "Conversion Missed", "PLAY", "STANDARD", "conversion_misses", 1, "NONE", maxMinute, player: "optional"),
            Team("PENALTY_GOAL", "Penalty Goal", "SCORING", "KEY", "score", 3, "REGULATION", maxMinute, player: "optional"),
            Team("PENALTY_MISSED", "Penalty Missed", "PLAY", "STANDARD", "penalty_misses", 1, "NONE", maxMinute, player: "optional"),
            Team("DROP_GOAL", "Drop Goal", "SCORING", "KEY", "score", 3, "REGULATION", maxMinute, player: "optional"),
            Team("YELLOW_CARD", "Yellow Card", "DISCIPLINE", "KEY", "yellow_cards", 1, "NONE", maxMinute, player: "optional", extra: new { sinBinMinutes }),
            Team("RED_CARD", "Red Card", "DISCIPLINE", "KEY", "red_cards", 1, "NONE", maxMinute, player: "optional"),
            Team("SIN_BIN_START", "Sin Bin Start", "DISCIPLINE", "KEY", "sin_bins", 1, "NONE", maxMinute, player: "optional", extra: new { sinBinMinutes }),
            Team("SIN_BIN_END", "Sin Bin End", "DISCIPLINE", "STANDARD", "sin_bin_returns", 1, "NONE", maxMinute, player: "optional", extra: new { sinBinMinutes }),
            Team("SUBSTITUTION", "Substitution", "PERSONNEL", "STANDARD", "substitutions", 1, "NONE", maxMinute, player: "required", secondaryPlayer: true, extra: new { maxSubstitutions }),
            Team("TEMPORARY_REPLACEMENT", "Temporary Replacement", "PERSONNEL", "STANDARD", "temporary_replacements", 1, "NONE", maxMinute, player: "required", secondaryPlayer: true),
            Team("INJURY", "Injury", "STOPPAGE", "KEY", "injuries", 1, "NONE", maxMinute, player: "optional"),
            System("TMO_REVIEW", "TMO Review", "REVIEW", "KEY", maxMinute),
            System("TMO_DECISION", "TMO Decision", "REVIEW", "KEY", maxMinute),
            System("MATCH_END", "Match End", "SYSTEM", "KEY", maxMinute),
            Team("SCRUM", "Scrum", "SET_PIECE", "DETAILED", "scrums", 1, "NONE", maxMinute),
            Team("LINEOUT", "Lineout", "SET_PIECE", "DETAILED", "lineouts", 1, "NONE", maxMinute),
            Team("TURNOVER", "Turnover", "PLAY", "DETAILED", "turnovers", 1, "NONE", maxMinute),
            Team("PENALTY", "Penalty", "DISCIPLINE", "DETAILED", "penalties", 1, "NONE", maxMinute),
            Team("FREE_KICK", "Free Kick", "PLAY", "DETAILED", "free_kicks", 1, "NONE", maxMinute)
        ];
    }

    public static RugbyTimelineEventSpec? Find(string eventType, string variant = RugbyVariants.Fifteens) =>
        Events(variant).Concat(Aliases(variant)).FirstOrDefault(item =>
            item.Id.Equals(eventType, StringComparison.OrdinalIgnoreCase));

    public static string CanonicalType(string eventType) => Normalize(eventType) switch
    {
        "CONVERSION" => "CONVERSION_MADE",
        "PENALTY_KICK" => "PENALTY_GOAL",
        "SIN_BIN" => "SIN_BIN_START",
        "SUBSTITUTION_IN" or "SUBSTITUTION_OUT" => "SUBSTITUTION",
        "LINE_OUT" => "LINEOUT",
        "REFEREE_DECISION" => "TMO_DECISION",
        "PENALTY_CONCEDED" => "PENALTY",
        _ => eventType
    };

    public static string GetDefinitionsJson(string variant = RugbyVariants.Fifteens) =>
        JsonSerializer.Serialize(Events(variant).Concat(Aliases(variant)), JsonOptions);

    private static IReadOnlyList<RugbyTimelineEventSpec> Aliases(string variant)
    {
        var events = Events(variant);
        RugbyTimelineEventSpec Alias(string id, string canonical) =>
            events.First(item => item.Id == canonical) with { Id = id };
        return
        [
            Alias("CONVERSION", "CONVERSION_MADE"),
            Alias("PENALTY_KICK", "PENALTY_GOAL"),
            Alias("SIN_BIN", "SIN_BIN_START"),
            Alias("SUBSTITUTION_IN", "SUBSTITUTION"),
            Alias("SUBSTITUTION_OUT", "SUBSTITUTION"),
            Alias("LINE_OUT", "LINEOUT"),
            Alias("REFEREE_DECISION", "TMO_DECISION"),
            Alias("PENALTY_CONCEDED", "PENALTY"),
            Alias("KNOCK_ON", "TURNOVER"),
            Alias("FORWARD_PASS", "TURNOVER"),
            Alias("HIGH_TACKLE", "PENALTY"),
            Alias("OFFSIDE", "PENALTY"),
            Alias("MAUL", "TURNOVER"),
            Alias("RUCK", "TURNOVER")
        ];
    }

    private static RugbyTimelineEventSpec System(
        string id, string name, string category, string importance, int maxMinute) =>
        new(id, name, category, importance, null, 0, false, false, false,
            ["*"], "TimestampOrClock", "ApprovalRequired", "NONE",
            Validation(0, maxMinute));

    private static RugbyTimelineEventSpec Team(
        string id, string name, string category, string importance,
        string metric, decimal value, string scoreEffect, int maxMinute,
        string player = "none", bool secondaryPlayer = false, object? extra = null) =>
        new(id, name, category, importance, metric, value, true,
            player == "required", secondaryPlayer, ["Half", "ExtraTime"],
            "TimestampOrClock", "ApprovalRequired", scoreEffect,
            Validation(value, maxMinute, extra));

    private static object Validation(decimal value, int maxMinute, object? extra = null)
    {
        var result = new Dictionary<string, object?>
        {
            ["minimumValue"] = value,
            ["maximumValue"] = value,
            ["minMinute"] = 0,
            ["maxMinute"] = maxMinute,
            ["minimumClockSeconds"] = 0,
            ["maximumClockSeconds"] = maxMinute * 60
        };
        if (extra is not null)
        {
            foreach (var property in extra.GetType().GetProperties())
                result[char.ToLowerInvariant(property.Name[0]) + property.Name[1..]] = property.GetValue(extra);
        }
        return result;
    }

    private static string Normalize(string value) => string.Join('_', value.Trim().ToUpperInvariant()
        .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries));
}
