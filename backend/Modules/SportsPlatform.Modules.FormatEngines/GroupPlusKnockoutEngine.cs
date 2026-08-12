using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Implementation of the group-plus-knockout format engine.
/// Generates a group stage followed by knockout stage.
/// </summary>
public class GroupPlusKnockoutEngine : IGroupPlusKnockoutEngine
{
    private readonly RoundRobinEngine _roundRobinEngine;
    private readonly KnockoutEngine _knockoutEngine;

    public GroupPlusKnockoutEngine()
    {
        _roundRobinEngine = new RoundRobinEngine();
        _knockoutEngine = new KnockoutEngine();
    }

    public string FormatType => "GroupPlusKnockout";

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null)
    {
        var groupKnockoutOptions = options as GroupPlusKnockoutOptions ?? new GroupPlusKnockoutOptions();
        return GenerateContests(stage, participants, groupKnockoutOptions);
    }

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        GroupPlusKnockoutOptions? options = null)
    {
        options ??= new GroupPlusKnockoutOptions();
        var participantList = participants.ToList();

        if (!options.SportId.HasValue || options.SportId == Guid.Empty)
            throw new ArgumentException("A sport ID is required to generate group contests.");
        if (options.GroupsCount < 2 || options.TeamsPerGroup < 2)
            throw new ArgumentOutOfRangeException(nameof(options), "At least two valid groups are required.");
        if (options.QualifiersPerGroup < 1 || options.QualifiersPerGroup >= options.TeamsPerGroup)
            throw new ArgumentOutOfRangeException(nameof(options), "The qualification count is invalid.");

        if (participantList.Count < options.TeamsPerGroup * options.GroupsCount)
        {
            throw new ArgumentException(
                $"Not enough participants. Need at least {options.TeamsPerGroup * options.GroupsCount} " +
                $"for {options.GroupsCount} groups of {options.TeamsPerGroup} teams.");
        }

        var results = new List<ContestGenerationResult>();

        // Generate group stage
        var groupResults = GenerateGroupStage(stage, participantList, options);
        results.AddRange(groupResults);

        return results;
    }

    public IEnumerable<ContestGenerationResult> GenerateGroupStage(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        GroupPlusKnockoutOptions options)
    {
        var participantList = participants.ToList();
        var results = new List<ContestGenerationResult>();
        var matchNumber = 1;

        // Distribute participants into groups
        var groups = DistributeIntoGroups(participantList, options.GroupsCount, options.TeamsPerGroup);

        foreach (var (groupName, groupParticipants) in groups)
        {
            // Add group to stage
            var group = stage.AddGroup(groupName);

            // Generate round-robin contests for this group
            var roundRobinOptions = new RoundRobinOptions
            {
                SportId = options.SportId,
                HomeAndAway = false, // Single round-robin in group stage
                BalanceVenues = true
            };

            var groupContests = _roundRobinEngine.GenerateContests(stage, groupParticipants, roundRobinOptions);

            foreach (var contestResult in groupContests)
            {
                contestResult.Contest.AssignToGroup(group.Id);
                results.Add(new ContestGenerationResult(
                    Contest: contestResult.Contest,
                    Round: contestResult.Round,
                    MatchNumber: matchNumber,
                    HomeParticipantId: contestResult.HomeParticipantId,
                    AwayParticipantId: contestResult.AwayParticipantId,
                    GroupName: groupName));

                matchNumber++;
            }
        }

        return results;
    }

    public IEnumerable<ContestGenerationResult> GenerateKnockoutStage(
        Stage stage,
        IEnumerable<GroupResult> groupResults,
        GroupPlusKnockoutOptions options)
    {
        var results = new List<ContestGenerationResult>();
        var matchNumber = 1;

        // Collect qualifiers from each group
        var qualifiers = new List<ParticipantInfo>();
        var groupResultList = groupResults.ToList();

        foreach (var group in groupResultList)
        {
            var topQualifiers = group.Standings
                .Take(options.QualifiersPerGroup)
                .Select((standing, index) => new ParticipantInfo(
                    standing.ParticipantId,
                    "Team",
                    index + 1,
                    group.GroupName));

            qualifiers.AddRange(topQualifiers);
        }

        // Add best third place if enabled
        if (options.IncludeBestThirdPlace && options.BestThirdPlaceCount > 0)
        {
            var allThirdPlaces = groupResultList
                .SelectMany(g => g.Standings.Skip(2).Take(1))
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.GoalsFor - s.GoalsAgainst)
                .Take(options.BestThirdPlaceCount);

            foreach (var thirdPlace in allThirdPlaces)
            {
                qualifiers.Add(new ParticipantInfo(
                    thirdPlace.ParticipantId,
                    "Team",
                    null,
                    "BestThird"));
            }
        }

        var knockoutOptions = new KnockoutOptions
        {
            SportId = options.SportId,
            RandomDraw = false, // Use seeded draw based on group positions
            IncludeThirdPlace = options.IncludeThirdPlacePlayoff
        };

        var seededQualifiers = qualifiers.Select((item, index) => item with { Seed = index + 1 });
        var knockoutContests = _knockoutEngine.GenerateContests(stage, seededQualifiers, knockoutOptions);

        foreach (var contestResult in knockoutContests)
        {
            results.Add(new ContestGenerationResult(
                Contest: contestResult.Contest,
                Round: contestResult.Round + 100, // Offset to separate from group stage
                MatchNumber: matchNumber,
                HomeParticipantId: contestResult.HomeParticipantId,
                AwayParticipantId: contestResult.AwayParticipantId,
                GroupName: "Knockout"));

            matchNumber++;
        }

        return results;
    }

    public int CalculateTotalContests(int participantCount, GroupPlusKnockoutOptions options)
    {
        // Group stage contests: C(n/g, 2) * g where g = number of groups
        var teamsPerGroup = participantCount / options.GroupsCount;
        var groupContests = options.GroupsCount * (teamsPerGroup * (teamsPerGroup - 1) / 2);

        // Knockout stage contests: n - 1 + (third place if enabled)
        var totalQualifiers = options.GroupsCount * options.QualifiersPerGroup;
        if (options.IncludeBestThirdPlace)
            totalQualifiers += options.BestThirdPlaceCount;

        var knockoutContests = totalQualifiers - 1;
        if (options.IncludeThirdPlacePlayoff)
            knockoutContests += 1;

        return groupContests + knockoutContests;
    }

    private List<(string GroupName, List<ParticipantInfo> Participants)> DistributeIntoGroups(
        List<ParticipantInfo> participants,
        int groupsCount,
        int teamsPerGroup)
    {
        var groups = new List<(string GroupName, List<ParticipantInfo> Participants)>();

        // Sort by seed if available
        var sorted = participants.OrderBy(p => p.Seed ?? int.MaxValue).ToList();

        // Distribute using serpentine method for balanced groups
        for (var i = 0; i < groupsCount; i++)
        {
            var groupName = $"Group {(char)('A' + i)}";
            var groupParticipants = new List<ParticipantInfo>();

            for (var j = 0; j < teamsPerGroup; j++)
            {
                var index = j * groupsCount + i;
                if (index < sorted.Count)
                {
                    groupParticipants.Add(sorted[index]);
                }
            }

            groups.Add((groupName, groupParticipants));
        }

        return groups;
    }
}
