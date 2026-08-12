using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for best-of-series format engine.
/// Handles multi-game series like best-of-3, best-of-5, best-of-7.
/// </summary>
public interface IBestOfSeriesEngine : IFormatEngine
{
    /// <summary>
    /// Generates contests for a best-of series.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        BestOfSeriesOptions? options = null);

    /// <summary>
    /// Calculates the number of games needed to win the series.
    /// </summary>
    int GamesToWin(int totalGames);

    /// <summary>
    /// Determines the series winner based on game results.
    /// </summary>
    SeriesResult? DetermineSeriesWinner(IEnumerable<Contest> games);
}

/// <summary>
/// Options specific to best-of-series format.
/// </summary>
public class BestOfSeriesOptions : FormatOptions
{
    /// <summary>
    /// Total number of games in the series (e.g., 3, 5, 7).
    /// </summary>
    public int TotalGames { get; set; } = 3;

    /// <summary>
    /// Whether home court/field alternates between games.
    /// </summary>
    public bool AlternateHomeGames { get; set; } = true;

    /// <summary>
    /// Optional per-game home order. 1 means the home-advantaged participant;
    /// 2 means the opponent. Its length must match TotalGames.
    /// </summary>
    public IReadOnlyList<int>? HomeSequence { get; set; }

    /// <summary>
    /// Which participant gets home advantage in odd-numbered games.
    /// </summary>
    public HomeAdvantage HomeAdvantage { get; set; } = HomeAdvantage.HigherSeed;

    /// <summary>
    /// Maximum number of overtime periods per game.
    /// </summary>
    public int MaxOvertimePeriods { get; set; } = 3;

    /// <summary>
    /// Whether to include a decisive game if tied after regulation.
    /// </summary>
    public bool RequireDecisiveGame { get; set; } = true;
}

/// <summary>
/// Determines home advantage assignment.
/// </summary>
public enum HomeAdvantage
{
    /// <summary>
    /// Higher seed (lower number) gets home advantage in odd games.
    /// </summary>
    HigherSeed,

    /// <summary>
    /// Lower seed (higher number) gets home advantage in odd games.
    /// </summary>
    LowerSeed,

    /// <summary>
    /// Home advantage alternates starting with participant 1.
    /// </summary>
    Alternate,

    /// <summary>
    /// No home advantage (neutral venue).
    /// </summary>
    Neutral
}

/// <summary>
/// Result of a series determination.
/// </summary>
public record SeriesResult(
    Guid WinnerId,
    int GamesWon,
    int GamesLost,
    int GamesPlayed,
    bool WentToFinalGame);
