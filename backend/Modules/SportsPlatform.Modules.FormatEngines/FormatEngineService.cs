using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Service for managing and executing format engines.
/// </summary>
public class FormatEngineService
{
    private readonly Dictionary<string, IFormatEngine> _engines;

    public FormatEngineService()
    {
        _engines = new Dictionary<string, IFormatEngine>
        {
            { "RoundRobin", new RoundRobinEngine() },
            { "Knockout", new KnockoutEngine() },
            { "GroupPlusKnockout", new GroupPlusKnockoutEngine() }
        };
    }

    /// <summary>
    /// Gets all available format types.
    /// </summary>
    public IEnumerable<string> GetAvailableFormats() => _engines.Keys;

    /// <summary>
    /// Gets a format engine by type.
    /// </summary>
    public IFormatEngine? GetEngine(string formatType)
    {
        return _engines.TryGetValue(formatType, out var engine) ? engine : null;
    }

    /// <summary>
    /// Generates contests using the specified format engine.
    /// </summary>
    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        string formatType,
        FormatOptions? options = null)
    {
        var engine = GetEngine(formatType);
        if (engine == null)
        {
            throw new ArgumentException($"Unknown format type: {formatType}");
        }

        return engine.GenerateContests(stage, participants, options);
    }

    /// <summary>
    /// Validates if a format can be applied with the given participants.
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateFormat(
        string formatType,
        int participantCount,
        FormatOptions? options = null)
    {
        if (participantCount < 2)
        {
            return (false, "At least 2 participants are required.");
        }

        return formatType switch
        {
            "RoundRobin" => (true, null),
            "Knockout" => ValidateKnockout(participantCount),
            "GroupPlusKnockout" => ValidateGroupPlusKnockout(participantCount, options as GroupPlusKnockoutOptions),
            _ => (false, $"Unknown format type: {formatType}")
        };
    }

    private (bool, string?) ValidateKnockout(int participantCount)
    {
        if (participantCount < 2)
        {
            return (false, "Knockout requires at least 2 participants.");
        }
        return (true, null);
    }

    private (bool, string?) ValidateGroupPlusKnockout(int participantCount, GroupPlusKnockoutOptions? options)
    {
        options ??= new GroupPlusKnockoutOptions();
        var required = options.TeamsPerGroup * options.GroupsCount;

        if (participantCount < required)
        {
            return (false, $"Group-plus-knockout requires at least {required} participants " +
                $"for {options.GroupsCount} groups of {options.TeamsPerGroup} teams.");
        }

        return (true, null);
    }

    /// <summary>
    /// Calculates the total number of contests for a given format.
    /// </summary>
    public int CalculateContestCount(string formatType, int participantCount, FormatOptions? options = null)
    {
        return formatType switch
        {
            "RoundRobin" => CalculateRoundRobinContests(participantCount, options as RoundRobinOptions),
            "Knockout" => CalculateKnockoutContests(participantCount, options as KnockoutOptions),
            "GroupPlusKnockout" => CalculateGroupPlusKnockoutContests(participantCount, options as GroupPlusKnockoutOptions),
            _ => 0
        };
    }

    private int CalculateRoundRobinContests(int participantCount, RoundRobinOptions? options)
    {
        options ??= new RoundRobinOptions();
        // Number of matches = n * (n-1) / 2 for single round-robin
        var matches = participantCount * (participantCount - 1) / 2;
        return options.HomeAndAway ? matches * 2 : matches;
    }

    private int CalculateKnockoutContests(int participantCount, KnockoutOptions? options)
    {
        options ??= new KnockoutOptions();
        // Number of matches = n - 1 (single elimination)
        var matches = participantCount - 1;
        return options.IncludeThirdPlace ? matches + 1 : matches;
    }

    private int CalculateGroupPlusKnockoutContests(int participantCount, GroupPlusKnockoutOptions? options)
    {
        options ??= new GroupPlusKnockoutOptions();
        var engine = new GroupPlusKnockoutEngine();
        return engine.CalculateTotalContests(participantCount, options);
    }
}