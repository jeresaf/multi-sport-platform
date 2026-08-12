using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for knockout (elimination) format engine.
/// </summary>
public interface IKnockoutEngine : IFormatEngine
{
    /// <summary>
    /// Generates knockout bracket contests.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        KnockoutOptions? options = null);

    /// <summary>
    /// Calculates the number of rounds needed for a knockout tournament.
    /// </summary>
    int CalculateRounds(int participantCount, bool includeThirdPlace);

    /// <summary>
    /// Determines the number of byes needed to fill the bracket to a power of 2.
    /// </summary>
    int CalculateByes(int participantCount);

    /// <summary>
    /// Generates the bracket structure for seeded participants.
    /// </summary>
    IEnumerable<BracketPosition> GenerateSeededBracket(
        IEnumerable<ParticipantInfo> participants,
        int rounds);
}

/// <summary>
/// Represents a position in the knockout bracket.
/// </summary>
public record BracketPosition(
    int Round,
    int Position,
    Guid? ParticipantId,
    bool IsBye,
    int? Seed = null);
