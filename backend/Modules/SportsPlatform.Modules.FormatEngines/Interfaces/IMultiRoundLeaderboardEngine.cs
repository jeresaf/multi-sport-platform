using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for multi-round leaderboard format engine.
/// Used for sports like golf, bowling, and track & field where participants
/// compete across multiple rounds with cumulative scoring.
/// </summary>
public interface IMultiRoundLeaderboardEngine : IFormatEngine
{
    /// <summary>
    /// Generates contests for each round in the leaderboard format.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        MultiRoundLeaderboardOptions? options = null);

    /// <summary>
    /// Calculates the leaderboard based on round scores.
    /// </summary>
    /// <param name="roundScores">Collection of round scores for all participants.</param>
    /// <param name="options">Leaderboard options including cut rules.</param>
    /// <returns>Complete leaderboard result with positions and cut line.</returns>
    LeaderboardResult CalculateLeaderboard(
        IEnumerable<RoundScore> roundScores,
        MultiRoundLeaderboardOptions? options = null);

    /// <summary>
    /// Applies the cut rule to determine which participants continue.
    /// </summary>
    /// <param name="participants">All participants.</param>
    /// <param name="roundScores">Scores after the cut round.</param>
    /// <param name="options">Leaderboard options with cut configuration.</param>
    /// <returns>Participants who made the cut.</returns>
    IEnumerable<ParticipantInfo> ApplyCut(
        IEnumerable<ParticipantInfo> participants,
        IEnumerable<RoundScore> roundScores,
        MultiRoundLeaderboardOptions? options = null);
}
