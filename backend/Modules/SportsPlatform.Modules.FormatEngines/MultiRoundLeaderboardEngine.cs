using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Implementation of the multi-round leaderboard format engine.
/// Generates individual contests for each round with cumulative scoring.
/// Used for sports like golf, bowling, and track & field.
/// </summary>
public class MultiRoundLeaderboardEngine : IMultiRoundLeaderboardEngine
{
    public string FormatType => "MultiRoundLeaderboard";

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null)
    {
        var leaderboardOptions = options as MultiRoundLeaderboardOptions ?? new MultiRoundLeaderboardOptions();
        return GenerateContests(stage, participants, leaderboardOptions);
    }

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        MultiRoundLeaderboardOptions? options = null)
    {
        options ??= new MultiRoundLeaderboardOptions();
        var participantList = participants.ToList();

        if (options.SportId is null || options.SportId == Guid.Empty)
            throw new ArgumentException("A sport ID is required.", nameof(options));
        if (participantList.Count < 2)
        {
            throw new ArgumentException("At least 2 participants are required for multi-round leaderboard format.");
        }

        var results = new List<ContestGenerationResult>();
        var matchNumber = 1;

        // Generate a contest for each round
        for (var round = 1; round <= options.TotalRounds; round++)
        {
            // For individual sports, each participant has their own contest
            // but we can also generate group contests
            if (options.GroupContestSize > 0)
            {
                // Generate group contests (e.g., tee times in golf)
                var groups = CreateGroups(participantList, options.GroupContestSize, round);
                var groupNumber = 1;

                foreach (var group in groups)
                {
                    var contest = CreateContest(stage, options.SportId.Value, round, matchNumber,
                        group.Select(p => p.ParticipantId).ToList(),
                        group.Select(p => p.ParticipantType).ToList(),
                        $"Group {groupNumber}");

                    results.Add(new ContestGenerationResult(
                        Contest: contest,
                        Round: round,
                        MatchNumber: matchNumber,
                        HomeParticipantId: group.FirstOrDefault()?.ParticipantId,
                        AwayParticipantId: null,
                        GroupName: $"Group {groupNumber}"));

                    matchNumber++;
                    groupNumber++;
                }
            }
            else
            {
                // Each participant gets their own individual contest
                foreach (var participant in participantList)
                {
                    var contest = CreateContest(stage, options.SportId.Value, round, matchNumber,
                        new List<Guid> { participant.ParticipantId },
                        new List<string> { participant.ParticipantType },
                        null);

                    results.Add(new ContestGenerationResult(
                        Contest: contest,
                        Round: round,
                        MatchNumber: matchNumber,
                        HomeParticipantId: participant.ParticipantId,
                        AwayParticipantId: null,
                        GroupName: null));

                    matchNumber++;
                }
            }
        }

        return results;
    }

    public LeaderboardResult CalculateLeaderboard(
        IEnumerable<RoundScore> roundScores,
        MultiRoundLeaderboardOptions? options = null)
    {
        options ??= new MultiRoundLeaderboardOptions();
        var scores = roundScores.ToList();

        ValidateOptions(options);
        if (scores.Any(score => score.Round < 1 || score.Round > options.TotalRounds))
            throw new ArgumentException("Every score must belong to a configured round.", nameof(roundScores));
        if (scores.GroupBy(score => new { score.ParticipantId, score.Round })
            .Any(group => group.Count() > 1))
            throw new ArgumentException("A participant can have only one score per round.", nameof(roundScores));

        if (scores.Count == 0)
            return new LeaderboardResult(new List<LeaderboardEntry>(), null, null);

        // Group scores by participant
        var participantScores = scores
            .GroupBy(s => s.ParticipantId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Round).ToList());

        var entries = new List<LeaderboardEntry>();
        var cutLine = options.CutAfterRound.HasValue ? options.CutToPlayers : (int?)null;
        var participantStatuses = participantScores.ToDictionary(
            item => item.Key,
            item => ResolveStatus(item.Value));
        var cumulativeRoundPositions = CalculateCumulativeRoundPositions(
            participantScores,
            participantStatuses,
            options);

        foreach (var kvp in participantScores)
        {
            var participantId = kvp.Key;
            var rounds = kvp.Value;
            var participantStatus = participantStatuses[participantId];

            // Check if participant made the cut
            var madeCut = participantStatus == LeaderboardParticipantStatus.Active;
            if (options.CutAfterRound.HasValue && cutLine.HasValue)
            {
                var roundsBeforeCut = rounds.Count(r => r.Round <= options.CutAfterRound.Value);
                if (roundsBeforeCut > 0)
                {
                    // Calculate position before cut
                    var scoresBeforeCut = participantScores
                        .Where(p => participantStatuses[p.Key] == LeaderboardParticipantStatus.Active
                            && p.Value.Any(r => r.Round <= options.CutAfterRound.Value))
                        .Select(p => new
                        {
                            ParticipantId = p.Key,
                            Score = p.Value
                                .Where(r => r.Round <= options.CutAfterRound.Value)
                                .Sum(r => r.Score)
                        })
                        .OrderBy(p => options.LowerIsBetter ? p.Score : -p.Score)
                        .ToList();

                    var cutoffIndex = Math.Min(cutLine.Value, scoresBeforeCut.Count) - 1;
                    var cutoffScore = cutoffIndex >= 0 ? scoresBeforeCut[cutoffIndex].Score : (int?)null;
                    var participantScore = scoresBeforeCut
                        .FirstOrDefault(p => p.ParticipantId == participantId);
                    madeCut = participantStatus == LeaderboardParticipantStatus.Active
                        && participantScore is not null
                        && cutoffScore.HasValue
                        && (options.LowerIsBetter
                            ? participantScore.Score <= cutoffScore.Value
                            : participantScore.Score >= cutoffScore.Value);
                }
            }

            // Calculate total score
            var totalScore = rounds.Sum(r => r.Score);
            var roundsPlayed = rounds.Count;

            // Calculate to-par (assuming standard par per round)
            var toPar = totalScore - (roundsPlayed * options.ParPerRound);

            entries.Add(new LeaderboardEntry(
                ParticipantId: participantId,
                Position: 0, // Will be calculated after sorting
                TotalScore: totalScore,
                ToPar: toPar,
                RoundsPlayed: roundsPlayed,
                RoundScores: rounds.ToDictionary(r => r.Round, r => r.Score),
                MadeCut: madeCut,
                Status: participantStatus switch
                {
                    LeaderboardParticipantStatus.Withdrawn => "Withdrawn",
                    LeaderboardParticipantStatus.Disqualified => "Disqualified",
                    _ when !madeCut => "MissedCut",
                    _ => "Active"
                },
                RoundPositions: cumulativeRoundPositions.GetValueOrDefault(participantId)
                    ?? new Dictionary<int, int>()
            ));
        }

        // Keep active qualifiers first, then missed-cut, withdrawn, and disqualified entries.
        var sortedEntries = (options.LowerIsBetter
                ? entries.OrderBy(StatusOrder).ThenBy(e => e.TotalScore)
                : entries.OrderBy(StatusOrder).ThenByDescending(e => e.TotalScore))
            .ThenBy(e => e.ParticipantId)
            .ToList();

        // Assign positions
        for (var i = 0; i < sortedEntries.Count; i++)
        {
            var entry = sortedEntries[i];
            var position = i + 1;

            // Handle ties
            if (i > 0)
            {
                var prevEntry = sortedEntries[i - 1];
                if (entry.Status == prevEntry.Status
                    && entry.TotalScore == prevEntry.TotalScore)
                {
                    position = prevEntry.Position;
                }
            }

            sortedEntries[i] = entry with { Position = position };
        }

        // Find leader and cut line
        var leader = sortedEntries.FirstOrDefault();
        var cutLineEntry = cutLine.HasValue && sortedEntries.Count >= cutLine.Value
            ? sortedEntries[cutLine.Value - 1]
            : null;

        return new LeaderboardResult(sortedEntries, leader, cutLineEntry);
    }

    public IEnumerable<ParticipantInfo> ApplyCut(
        IEnumerable<ParticipantInfo> participants,
        IEnumerable<RoundScore> roundScores,
        MultiRoundLeaderboardOptions? options = null)
    {
        options ??= new MultiRoundLeaderboardOptions();
        ValidateOptions(options);

        if (!options.CutAfterRound.HasValue || !options.CutToPlayers.HasValue)
            return participants;

        var scores = roundScores.ToList();
        var participantList = participants.ToList();

        // Calculate scores after cut round
        var scoresAfterCut = scores
            .Where(s => s.Round <= options.CutAfterRound.Value)
            .GroupBy(s => s.ParticipantId)
            .Where(group => ResolveStatus(group) == LeaderboardParticipantStatus.Active)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Score));

        if (options.CutToPlayers.Value <= 0 || scoresAfterCut.Count == 0)
            return Enumerable.Empty<ParticipantInfo>();

        var orderedScores = options.LowerIsBetter
            ? scoresAfterCut.OrderBy(item => item.Value).ThenBy(item => item.Key).ToArray()
            : scoresAfterCut.OrderByDescending(item => item.Value).ThenBy(item => item.Key).ToArray();
        var cutoffIndex = Math.Min(options.CutToPlayers.Value, orderedScores.Length) - 1;
        var cutoffScore = orderedScores[cutoffIndex].Value;
        var qualified = orderedScores
            .Where(item => options.LowerIsBetter
                ? item.Value <= cutoffScore
                : item.Value >= cutoffScore)
            .Select(item => item.Key)
            .ToHashSet();

        return participantList.Where(p => qualified.Contains(p.ParticipantId));
    }

    private static Dictionary<Guid, Dictionary<int, int>> CalculateCumulativeRoundPositions(
        IReadOnlyDictionary<Guid, List<RoundScore>> participantScores,
        IReadOnlyDictionary<Guid, LeaderboardParticipantStatus> participantStatuses,
        MultiRoundLeaderboardOptions options)
    {
        var result = participantScores.Keys.ToDictionary(
            participantId => participantId,
            _ => new Dictionary<int, int>());

        for (var round = 1; round <= options.TotalRounds; round++)
        {
            var cumulative = participantScores
                .Where(item => participantStatuses[item.Key] == LeaderboardParticipantStatus.Active
                    && item.Value.Any(score => score.Round == round))
                .Select(item => new
                {
                    ParticipantId = item.Key,
                    Score = item.Value.Where(score => score.Round <= round).Sum(score => score.Score)
                });
            var ordered = (options.LowerIsBetter
                    ? cumulative.OrderBy(item => item.Score)
                    : cumulative.OrderByDescending(item => item.Score))
                .ThenBy(item => item.ParticipantId)
                .ToArray();

            int? previousScore = null;
            var previousPosition = 0;
            for (var index = 0; index < ordered.Length; index++)
            {
                var position = previousScore == ordered[index].Score
                    ? previousPosition
                    : index + 1;
                result[ordered[index].ParticipantId][round] = position;
                previousScore = ordered[index].Score;
                previousPosition = position;
            }
        }

        return result;
    }

    private static LeaderboardParticipantStatus ResolveStatus(IEnumerable<RoundScore> scores)
    {
        var statuses = scores.Select(score => score.Status).ToHashSet();
        if (statuses.Contains(LeaderboardParticipantStatus.Disqualified))
            return LeaderboardParticipantStatus.Disqualified;
        return statuses.Contains(LeaderboardParticipantStatus.Withdrawn)
            ? LeaderboardParticipantStatus.Withdrawn
            : LeaderboardParticipantStatus.Active;
    }

    private static int StatusOrder(LeaderboardEntry entry) => entry.Status switch
    {
        "Active" => 0,
        "MissedCut" => 1,
        "Withdrawn" => 2,
        _ => 3
    };

    private static void ValidateOptions(MultiRoundLeaderboardOptions options)
    {
        if (options.TotalRounds <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Total rounds must be positive.");
        if (options.CutAfterRound is <= 0
            || options.CutAfterRound > options.TotalRounds)
            throw new ArgumentOutOfRangeException(nameof(options), "The cut round must be within the configured rounds.");
        if (options.CutAfterRound.HasValue && options.CutToPlayers is <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The cut size must be positive.");
    }

    private List<List<ParticipantInfo>> CreateGroups(
        List<ParticipantInfo> participants,
        int groupSize,
        int round)
    {
        var groups = new List<List<ParticipantInfo>>();
        var shuffled = participants.OrderBy(p => Guid.NewGuid()).ToList();

        for (var i = 0; i < shuffled.Count; i += groupSize)
        {
            var group = shuffled.Skip(i).Take(groupSize).ToList();
            groups.Add(group);
        }

        return groups;
    }

    private Contest CreateContest(Stage stage, Guid sportId, int round, int matchNumber,
        List<Guid> participantIds, List<string> participantTypes, string? groupName)
    {
        var contest = new Contest(
            Guid.NewGuid(),
            stage.TenantId,
            sportId,
            "MultiRound");
        contest.AssignToStage(stage.Id, matchNumber);

        var contestName = string.IsNullOrEmpty(groupName)
            ? $"Round {round} - Contest {matchNumber}"
            : groupName;
        contest.SetName(contestName);

        for (var i = 0; i < participantIds.Count; i++)
        {
            contest.AddParticipant(
                participantIds[i],
                i < participantTypes.Count ? participantTypes[i] : "Athlete",
                i + 1);
        }

        return contest;
    }
}

/// <summary>
/// Options specific to multi-round leaderboard format.
/// </summary>
public class MultiRoundLeaderboardOptions : FormatOptions
{
    /// <summary>
    /// Total number of rounds in the competition.
    /// </summary>
    public int TotalRounds { get; set; } = 4;

    /// <summary>
    /// Standard par score for each round (used for to-par calculation).
    /// </summary>
    public int ParPerRound { get; set; } = 72;

    /// <summary>
    /// If true, lower scores are better (golf). If false, higher scores are better.
    /// </summary>
    public bool LowerIsBetter { get; set; } = true;

    /// <summary>
    /// Round number after which to apply a cut. Null means no cut.
    /// </summary>
    public int? CutAfterRound { get; set; } = 2;

    /// <summary>
    /// Number of players to keep after the cut.
    /// </summary>
    public int? CutToPlayers { get; set; } = 70;

    /// <summary>
    /// Group size for generating contests (e.g., tee times in golf).
    /// 0 means each participant has their own contest.
    /// </summary>
    public int GroupContestSize { get; set; } = 4;

    /// <summary>
    /// Whether to randomize group assignments each round.
    /// </summary>
    public bool RandomizeGroups { get; set; } = true;
}

/// <summary>
/// Score for a specific round.
/// </summary>
public record RoundScore(
    Guid ParticipantId,
    int Round,
    int Score,
    LeaderboardParticipantStatus Status = LeaderboardParticipantStatus.Active);

public enum LeaderboardParticipantStatus
{
    Active = 0,
    Withdrawn = 1,
    Disqualified = 2
}

/// <summary>
/// Entry in the leaderboard.
/// </summary>
public record LeaderboardEntry(
    Guid ParticipantId,
    int Position,
    int TotalScore,
    int ToPar,
    int RoundsPlayed,
    Dictionary<int, int> RoundScores,
    bool MadeCut,
    string Status,
    Dictionary<int, int> RoundPositions);

/// <summary>
/// Complete leaderboard result.
/// </summary>
public record LeaderboardResult(
    List<LeaderboardEntry> Entries,
    LeaderboardEntry? Leader,
    LeaderboardEntry? CutLine);
