using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for group-plus-knockout format engine.
/// </summary>
public interface IGroupPlusKnockoutEngine : IFormatEngine
{
    /// <summary>
    /// Generates contests for a group stage followed by knockout stage.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        GroupPlusKnockoutOptions? options = null);

    /// <summary>
    /// Generates the group stage contests.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateGroupStage(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        GroupPlusKnockoutOptions options);

    /// <summary>
    /// Generates the knockout stage contests based on group results.
    /// </summary>
    IEnumerable<ContestGenerationResult> GenerateKnockoutStage(
        Stage stage,
        IEnumerable<GroupResult> groupResults,
        GroupPlusKnockoutOptions options);

    /// <summary>
    /// Calculates the total number of contests for a group-plus-knockout format.
    /// </summary>
    int CalculateTotalContests(int participantCount, GroupPlusKnockoutOptions options);
}

/// <summary>
/// Represents the results of a group stage.
/// </summary>
public record GroupResult(
    string GroupName,
    IReadOnlyList<GroupStanding> Standings);

/// <summary>
/// Represents a standing within a group.
/// </summary>
public record GroupStanding(
    int Position,
    Guid ParticipantId,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int Points);
