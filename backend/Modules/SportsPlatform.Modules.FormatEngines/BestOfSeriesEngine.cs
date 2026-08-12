using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Implementation of the best-of-series format engine.
/// Generates a series of games (e.g., best-of-3, best-of-5, best-of-7).
/// </summary>
public class BestOfSeriesEngine : IBestOfSeriesEngine
{
    public string FormatType => "BestOfSeries";

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null)
    {
        var seriesOptions = options as BestOfSeriesOptions ?? new BestOfSeriesOptions();
        return GenerateContests(stage, participants, seriesOptions);
    }

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        BestOfSeriesOptions? options = null)
    {
        options ??= new BestOfSeriesOptions();
        var participantList = participants.ToList();

        if (options.SportId is null || options.SportId == Guid.Empty)
            throw new ArgumentException("A sport ID is required.", nameof(options));
        if (participantList.Count != 2)
        {
            throw new ArgumentException("Best-of-series format requires exactly 2 participants.");
        }

        var results = new List<ContestGenerationResult>();
        var totalGames = options.TotalGames;
        if (totalGames < 1 || totalGames % 2 == 0)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The total number of series games must be a positive odd number.");
        if (options.HomeSequence is not null
            && (options.HomeSequence.Count != totalGames
                || options.HomeSequence.Any(slot => slot is not 1 and not 2)))
            throw new ArgumentException(
                "The home sequence must contain one valid participant slot per game.",
                nameof(options));
        var gamesToWin = GamesToWin(totalGames);
        var seriesId = Guid.NewGuid();

        // Determine home/away assignment based on options
        var homeAdvantage = DetermineHomeAdvantage(participantList, options);

        for (var gameNumber = 1; gameNumber <= totalGames; gameNumber++)
        {
            var isOddGame = gameNumber % 2 == 1;
            Guid? homeId = null;
            Guid? awayId = null;

            if (options.HomeSequence is not null)
            {
                var primaryIsHome = options.HomeSequence[gameNumber - 1] == 1;
                var homeIndex = primaryIsHome ? homeAdvantage : 1 - homeAdvantage;
                var awayIndex = 1 - homeIndex;
                homeId = participantList[homeIndex].ParticipantId;
                awayId = participantList[awayIndex].ParticipantId;
            }
            else if (options.AlternateHomeGames)
            {
                if (isOddGame)
                {
                    // Odd games: home based on home advantage setting
                    (homeId, awayId) = homeAdvantage == 0
                        ? (participantList[0].ParticipantId, participantList[1].ParticipantId)
                        : (participantList[1].ParticipantId, participantList[0].ParticipantId);
                }
                else
                {
                    // Even games: reverse home/away
                    (homeId, awayId) = homeAdvantage == 0
                        ? (participantList[1].ParticipantId, participantList[0].ParticipantId)
                        : (participantList[0].ParticipantId, participantList[1].ParticipantId);
                }
            }
            else
            {
                // No alternation - same home team every game
                homeId = homeAdvantage == 0
                    ? participantList[0].ParticipantId
                    : participantList[1].ParticipantId;
                awayId = homeAdvantage == 0
                    ? participantList[1].ParticipantId
                    : participantList[0].ParticipantId;
            }

            var contest = CreateContest(stage, options.SportId.Value, gameNumber, homeId, awayId,
                participantList[0].ParticipantType,
                participantList[1].ParticipantType);
            contest.AssignToSeries(
                seriesId,
                gameNumber,
                totalGames,
                homeId == participantList[0].ParticipantId ? 1 : 2);

            results.Add(new ContestGenerationResult(
                Contest: contest,
                Round: 1,
                MatchNumber: gameNumber,
                HomeParticipantId: homeId,
                AwayParticipantId: awayId,
                GroupName: null));
        }

        return results;
    }

    public int GamesToWin(int totalGames)
    {
        return (totalGames / 2) + 1;
    }

    public SeriesResult? DetermineSeriesWinner(IEnumerable<Contest> games)
    {
        var completedGames = games
            .Where(g => g.Status == ContestStatus.Completed)
            .OrderBy(g => g.StartTime)
            .ToList();

        if (completedGames.Count == 0)
            return null;

        // Count wins for each participant
        var participantWins = new Dictionary<Guid, int>();
        foreach (var game in completedGames)
        {
            if (game.WinnerId.HasValue)
            {
                if (!participantWins.ContainsKey(game.WinnerId.Value))
                    participantWins[game.WinnerId.Value] = 0;
                participantWins[game.WinnerId.Value]++;
            }
        }

        if (participantWins.Count == 0)
            return null;

        // Find the winner (participant with most wins)
        var winner = participantWins.OrderByDescending(kv => kv.Value).First();
        var loser = participantWins.OrderByDescending(kv => kv.Value).Skip(1).FirstOrDefault();

        var bestOf = games
            .Select(game => game.SeriesBestOf)
            .FirstOrDefault(value => value.HasValue);
        var gamesToWin = GamesToWin(bestOf ?? completedGames.Count);
        if (winner.Value < gamesToWin)
            return null;
        var wentToFinalGame = participantWins.Values.Max() == gamesToWin && 
                              completedGames.Count == (gamesToWin * 2) - 1;

        return new SeriesResult(
            WinnerId: winner.Key,
            GamesWon: winner.Value,
            GamesLost: loser.Value,
            GamesPlayed: completedGames.Count,
            WentToFinalGame: wentToFinalGame);
    }

    /// <summary>
    /// Determines which participant gets home advantage in odd games.
    /// Returns 0 if participant 1 (first in list) gets home, 1 if participant 2 gets home.
    /// </summary>
    private int DetermineHomeAdvantage(List<ParticipantInfo> participants, BestOfSeriesOptions options)
    {
        return options.HomeAdvantage switch
        {
            HomeAdvantage.HigherSeed => (participants[0].Seed ?? int.MaxValue) <= (participants[1].Seed ?? int.MaxValue) ? 0 : 1,
            HomeAdvantage.LowerSeed => (participants[0].Seed ?? int.MaxValue) <= (participants[1].Seed ?? int.MaxValue) ? 1 : 0,
            HomeAdvantage.Alternate => 0, // Participant 1 starts with home
            HomeAdvantage.Neutral => 0, // Default, but won't be used
            _ => 0
        };
    }

    private Contest CreateContest(Stage stage, Guid sportId, int gameNumber,
        Guid? homeId, Guid? awayId, string? homeType, string? awayType)
    {
        var contest = new Contest(
            Guid.NewGuid(),
            stage.TenantId,
            sportId,
            "SeriesGame");
        contest.AssignToStage(stage.Id, gameNumber);

        contest.SetName($"Game {gameNumber}");

        if (homeId.HasValue)
            contest.AddParticipant(homeId.Value, homeType ?? "Team", 1);
        if (awayId.HasValue)
            contest.AddParticipant(awayId.Value, awayType ?? "Team", 2);

        return contest;
    }
}
