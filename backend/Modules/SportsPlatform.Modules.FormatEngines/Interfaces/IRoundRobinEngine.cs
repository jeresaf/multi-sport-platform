using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for round-robin format engine.
/// </summary>
public interface IRoundRobinEngine : IFormatEngine
{
    /// <summary>
    /// Generates round-robin contests ensuring each participant plays against every other.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        RoundRobinOptions? options = null);

    /// <summary>
    /// Calculates the number of rounds needed for a round-robin tournament.
    /// </summary>
    int CalculateRounds(int participantCount, bool doubleRoundRobin);
}
