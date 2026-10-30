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
            TieResolutions: Set("DRAW", "EXTRA_PERIODS_THEN_SHOOTOUT", "REPEAT_EXTRA_PERIOD", "EXTRA_PERIODS_SUDDEN_DEATH", "SHOOTOUT")),
    };

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
}

public sealed record ScoringFamily(IReadOnlySet<string> ScoreComponents, IReadOnlySet<string> TieResolutions);
