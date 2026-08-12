using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for competition format engines that generate contests based on different formats.
/// </summary>
public interface IFormatEngine
{
    /// <summary>
    /// Gets the format type this engine handles.
    /// </summary>
    string FormatType { get; }

    /// <summary>
    /// Generates contests for the given stage and participants.
    /// </summary>
    /// <param name="stage">The stage to generate contests for.</param>
    /// <param name="participants">The participants to include in contests.</param>
    /// <param name="options">Format-specific options.</param>
    /// <returns>A collection of contest generation results.</returns>
    IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null);
}

/// <summary>
/// Information about a participant for contest generation.
/// </summary>
public record ParticipantInfo(
    Guid ParticipantId,
    string ParticipantType,
    int? Seed = null,
    string? GroupName = null);

/// <summary>
/// Result of contest generation containing the contest and its metadata.
/// </summary>
public record ContestGenerationResult(
    Contest Contest,
    int Round,
    int MatchNumber,
    Guid? HomeParticipantId,
    Guid? AwayParticipantId,
    string? GroupName = null,
    Guid? TieId = null,
    int LegNumber = 1,
    bool IsThirdPlace = false);

/// <summary>
/// Base options for format engines.
/// </summary>
public class FormatOptions
{
    public Guid? SportId { get; set; }
    public bool IsDoubleRoundRobin { get; set; }
    public bool RandomizeDraw { get; set; }
    public bool IncludeThirdPlacePlayoff { get; set; }
    public int? ByesForSeededDraw { get; set; }
    public int ContestsPerRound { get; set; } = 1;
}

/// <summary>
/// Options specific to round-robin format.
/// </summary>
public class RoundRobinOptions : FormatOptions
{
    public bool HomeAndAway { get; set; } = true;
    public bool BalanceVenues { get; set; } = true;
    public int? DrawSeed { get; set; }
}

/// <summary>
/// Options specific to knockout format.
/// </summary>
public class KnockoutOptions : FormatOptions
{
    public bool RandomDraw { get; set; } = true;
    public bool IncludeThirdPlace { get; set; } = false;
    public bool IncludeConsolation { get; set; } = false;
    public int LegsPerTie { get; set; } = 1;
    public int? DrawSeed { get; set; }
}

/// <summary>
/// Options specific to group-plus-knockout format.
/// </summary>
public class GroupPlusKnockoutOptions : FormatOptions
{
    public int TeamsPerGroup { get; set; } = 4;
    public int GroupsCount { get; set; } = 2;
    public int QualifiersPerGroup { get; set; } = 2;
    public bool IncludeBestThirdPlace { get; set; } = false;
    public int BestThirdPlaceCount { get; set; } = 0;
}
