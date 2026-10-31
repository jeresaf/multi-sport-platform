namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// What each scoring family's engine understands: the score components beyond <c>main</c> and the tie resolutions
/// (docs/architecture/sport-engine.md §5). A family without an entry has no engine yet, so its definitions are refused.
/// </summary>
public static class ScoringFamilies
{
    public const string Additive = "ADDITIVE";

    public const string MainComponent = "main";

    public static IReadOnlyDictionary<string, ScoringFamily> Known { get; } = new Dictionary<string, ScoringFamily>(StringComparer.Ordinal)
    {
        [Additive] = new(
            ScoreComponents: Set(MainComponent, "shootout"),
            TieResolutions: Set(
                TieResolutions.Draw,
                TieResolutions.ExtraPeriodsThenShootout,
                TieResolutions.RepeatExtraPeriod,
                TieResolutions.ExtraPeriodsSuddenDeath,
                TieResolutions.Shootout)),
    };

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
}

public sealed record ScoringFamily(IReadOnlySet<string> ScoreComponents, IReadOnlySet<string> TieResolutions);

/// <summary>The rules' <c>contest.tie_resolution</c> values (sport-engine.md §5, "Outcome").</summary>
public static class TieResolutions
{
    public const string Draw = "DRAW";

    public const string ExtraPeriodsThenShootout = "EXTRA_PERIODS_THEN_SHOOTOUT";

    public const string RepeatExtraPeriod = "REPEAT_EXTRA_PERIOD";

    public const string ExtraPeriodsSuddenDeath = "EXTRA_PERIODS_SUDDEN_DEATH";

    public const string Shootout = "SHOOTOUT";
}
