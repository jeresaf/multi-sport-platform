using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Standings.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Deterministic School Games basketball demo with a completed regular season
/// and resumable best-of-three playoff series.
/// </summary>
public static class BasketballDemoSeedData
{
    public static readonly Guid CompetitionId = DemoId(100);
    public static readonly Guid BracketId = DemoId(4000);

    private static readonly Guid SystemAdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    private static readonly (string Name, string ShortName, string Venue)[] TeamDefinitions =
    {
        ("Nairobi Academy Falcons", "Falcons", "Nairobi Academy Court"),
        ("Mombasa High Sharks", "Sharks", "Mombasa High Gym"),
        ("Kisumu School Lakers", "Lakers", "Kisumu School Arena"),
        ("Nakuru College Rhinos", "Rhinos", "Nakuru College Court"),
        ("Eldoret Prep Striders", "Striders", "Eldoret Prep Gym"),
        ("Thika Scholars Titans", "Titans", "Thika Scholars Arena")
    };

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (await context.Competitions.AnyAsync(
                item => item.Id == CompetitionId,
                cancellationToken))
        {
            return;
        }

        var basketball = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "BASKETBALL", cancellationToken);
        if (basketball is null) return;

        var rulesetVersionId = basketball.Versions
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();
        var teamIds = Enumerable.Range(0, TeamDefinitions.Length)
            .Select(index => DemoId(200 + index))
            .ToArray();
        var existingTeams = await context.Participants
            .OfType<Team>()
            .Where(item => teamIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        for (var index = 0; index < TeamDefinitions.Length; index++)
        {
            var teamId = teamIds[index];
            if (existingTeams.ContainsKey(teamId)) continue;
            var definition = TeamDefinitions[index];
            var team = new Team(
                teamId,
                DemoSeedData.SchoolGamesTenantId,
                definition.Name,
                definition.ShortName);
            team.UpdateDetails(
                definition.Venue,
                $"School Coach {index + 1}",
                $"School Athletics Director {index + 1}");
            context.Participants.Add(team);
            existingTeams.Add(teamId, team);
        }

        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            CompetitionId,
            DemoSeedData.SchoolGamesTenantId,
            "National School Basketball Championship Demo",
            basketball.Id);
        if (rulesetVersionId.HasValue) competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();
        var edition = competition.AddEdition(
            "2030 School Games",
            new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 10, 31, 0, 0, 0, DateTimeKind.Utc));
        edition.Open();
        var regularSeason = edition.AddStage("Regular Season", StageType.League, 1);
        regularSeason.Open();
        var leagueGroup = regularSeason.AddGroup("School Basketball Table");
        var playoffStage = edition.AddStage("Top Four Playoffs", StageType.Knockout, 2);
        playoffStage.Open();
        context.Competitions.Add(competition);

        var stats = teamIds.ToDictionary(item => item, _ => new TeamStats());
        var regularSeasonContests = new List<Contest>();
        var results = new List<ContestResult>();
        var fixtureIndex = 0;

        for (var homeIndex = 0; homeIndex < teamIds.Length - 1; homeIndex++)
        {
            for (var awayIndex = homeIndex + 1; awayIndex < teamIds.Length; awayIndex++)
            {
                fixtureIndex++;
                var homeId = teamIds[homeIndex];
                var awayId = teamIds[awayIndex];
                var homeScore = 64 + ((homeIndex * 11 + awayIndex * 7) % 29);
                var awayScore = 61 + ((awayIndex * 13 + homeIndex * 5) % 27);
                if (homeScore == awayScore) homeScore += 3;
                var contestId = DemoId(1000 + fixtureIndex);
                var contest = CreateContest(
                    contestId,
                    basketball.Id,
                    rulesetVersionId,
                    regularSeason.Id,
                    leagueGroup.Id,
                    fixtureIndex,
                    $"{TeamDefinitions[homeIndex].Name} vs {TeamDefinitions[awayIndex].Name}",
                    TeamDefinitions[homeIndex].Venue,
                    new DateTime(2030, 7, 6, 12, 0, 0, DateTimeKind.Utc)
                        .AddDays((fixtureIndex - 1) * 4),
                    homeId,
                    awayId,
                    "TeamVsTeam");
                CompleteContest(contest, homeScore > awayScore ? homeId : awayId);
                regularSeasonContests.Add(contest);
                ApplyResult(stats[homeId], stats[awayId], homeScore, awayScore);
                AddPublishedResults(
                    results,
                    contestId,
                    fixtureIndex,
                    homeId,
                    homeScore,
                    awayId,
                    awayScore,
                    2000 + ((fixtureIndex - 1) * 2),
                    5000 + fixtureIndex);
            }
        }

        context.Contests.AddRange(regularSeasonContests);
        context.ContestResults.AddRange(results);

        var rankedTeamIds = stats
            .OrderByDescending(item => item.Value.Won)
            .ThenByDescending(item => item.Value.PointsFor - item.Value.PointsAgainst)
            .ThenByDescending(item => item.Value.PointsFor)
            .ThenBy(item => Array.IndexOf(teamIds, item.Key))
            .Select(item => item.Key)
            .ToArray();
        for (var index = 0; index < rankedTeamIds.Length; index++)
        {
            var teamId = rankedTeamIds[index];
            var teamStats = stats[teamId];
            var standing = new Standing(
                DemoId(3001 + index),
                CompetitionId,
                leagueGroup.Id,
                teamId);
            standing.UpdateStats(
                teamStats.Played,
                teamStats.Won,
                0,
                teamStats.Lost,
                teamStats.PointsFor,
                teamStats.PointsAgainst,
                teamStats.Won);
            standing.SetPosition(index + 1);
            context.Standings.Add(standing);
        }

        AddPlayoffSeries(
            context,
            basketball.Id,
            rulesetVersionId,
            playoffStage.Id,
            rankedTeamIds);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddPlayoffSeries(
        SportsPlatformDbContext context,
        Guid sportId,
        Guid? rulesetVersionId,
        Guid stageId,
        IReadOnlyList<Guid> rankedTeamIds)
    {
        var semifinalSeries1Id = DemoId(4101);
        var semifinalSeries2Id = DemoId(4102);
        var finalSeriesId = DemoId(4103);
        var semifinal1 = CreateSeries(
            sportId,
            rulesetVersionId,
            stageId,
            semifinalSeries1Id,
            1101,
            "Semifinal 1",
            rankedTeamIds[0],
            rankedTeamIds[3],
            new DateTime(2030, 10, 5, 14, 0, 0, DateTimeKind.Utc));
        var semifinal2 = CreateSeries(
            sportId,
            rulesetVersionId,
            stageId,
            semifinalSeries2Id,
            1104,
            "Semifinal 2",
            rankedTeamIds[1],
            rankedTeamIds[2],
            new DateTime(2030, 10, 6, 14, 0, 0, DateTimeKind.Utc));
        var finals = CreateSeries(
            sportId,
            rulesetVersionId,
            stageId,
            finalSeriesId,
            1107,
            "Championship Final",
            null,
            null,
            new DateTime(2030, 10, 19, 14, 0, 0, DateTimeKind.Utc));

        var playoffResults = new List<ContestResult>();
        CompleteContest(semifinal1[0], rankedTeamIds[0]);
        AddPublishedResults(
            playoffResults,
            semifinal1[0].Id,
            101,
            rankedTeamIds[0],
            88,
            rankedTeamIds[3],
            76,
            2200,
            5201);
        CompleteContest(semifinal2[0], rankedTeamIds[1]);
        AddPublishedResults(
            playoffResults,
            semifinal2[0].Id,
            102,
            rankedTeamIds[1],
            81,
            rankedTeamIds[2],
            74,
            2210,
            5202);

        context.Contests.AddRange(semifinal1.Concat(semifinal2).Concat(finals));
        context.ContestResults.AddRange(playoffResults);

        var bracket = new Bracket(
            BracketId,
            CompetitionId,
            "School Games Best-of-Three Playoffs",
            BracketType.SingleElimination,
            4);
        bracket.SetSeeded(true);
        bracket.SetDrawSeed(2030);
        var semifinalRound = new BracketRound(
            DemoId(4010), bracket.Id, 1, "Best-of-3 Semifinals", 2);
        semifinalRound.SetScheduledAt(
            new DateTime(2030, 10, 5, 14, 0, 0, DateTimeKind.Utc));
        var finalRound = new BracketRound(
            DemoId(4020), bracket.Id, 2, "Best-of-3 Final", 1);
        finalRound.SetScheduledAt(
            new DateTime(2030, 10, 19, 14, 0, 0, DateTimeKind.Utc));
        var finalMatch = new BracketMatch(DemoId(4033), finalRound.Id, 1);
        finalMatch.BindSeries(finalSeriesId, finals[0].Id, 3);

        var semifinalMatch1 = new BracketMatch(DemoId(4031), semifinalRound.Id, 1);
        semifinalMatch1.SetParticipants(rankedTeamIds[0], rankedTeamIds[3]);
        semifinalMatch1.BindSeries(semifinalSeries1Id, semifinal1[0].Id, 3);
        semifinalMatch1.SetScore("1", "0");
        semifinalMatch1.AdvanceTo(finalMatch.Id, 1);
        var semifinalMatch2 = new BracketMatch(DemoId(4032), semifinalRound.Id, 2);
        semifinalMatch2.SetParticipants(rankedTeamIds[1], rankedTeamIds[2]);
        semifinalMatch2.BindSeries(semifinalSeries2Id, semifinal2[0].Id, 3);
        semifinalMatch2.SetScore("1", "0");
        semifinalMatch2.AdvanceTo(finalMatch.Id, 2);

        semifinalRound.AddMatch(semifinalMatch1);
        semifinalRound.AddMatch(semifinalMatch2);
        finalRound.AddMatch(finalMatch);
        bracket.AddRound(semifinalRound);
        bracket.AddRound(finalRound);
        bracket.Publish();
        bracket.Start();
        context.Brackets.Add(bracket);
    }

    private static Contest[] CreateSeries(
        Guid sportId,
        Guid? rulesetVersionId,
        Guid stageId,
        Guid seriesId,
        int firstContestNumber,
        string name,
        Guid? participant1Id,
        Guid? participant2Id,
        DateTime firstGame)
    {
        var games = new Contest[3];
        var homeSequence = new[] { 1, 2, 1 };
        for (var gameNumber = 1; gameNumber <= 3; gameNumber++)
        {
            var homeSlot = homeSequence[gameNumber - 1];
            var homeId = homeSlot == 1 ? participant1Id : participant2Id;
            var awayId = homeSlot == 1 ? participant2Id : participant1Id;
            var contest = CreateContest(
                DemoId(firstContestNumber + gameNumber - 1),
                sportId,
                rulesetVersionId,
                stageId,
                null,
                firstContestNumber + gameNumber - 1,
                $"{name} — Game {gameNumber}",
                "School Games Championship Arena",
                firstGame.AddDays((gameNumber - 1) * 2),
                homeId,
                awayId,
                "SeriesGame");
            contest.AssignToSeries(seriesId, gameNumber, 3, homeSlot);
            games[gameNumber - 1] = contest;
        }

        return games;
    }

    private static Contest CreateContest(
        Guid id,
        Guid sportId,
        Guid? rulesetVersionId,
        Guid stageId,
        Guid? groupId,
        int sortOrder,
        string name,
        string venue,
        DateTime startTime,
        Guid? homeId,
        Guid? awayId,
        string contestShape)
    {
        var contest = new Contest(
            id,
            DemoSeedData.SchoolGamesTenantId,
            sportId,
            contestShape);
        contest.SetName(name);
        if (rulesetVersionId.HasValue) contest.AssignRulesetVersion(rulesetVersionId.Value);
        contest.AssignToStage(stageId, sortOrder);
        if (groupId.HasValue) contest.AssignToGroup(groupId.Value);
        if (homeId.HasValue) contest.AddParticipant(homeId.Value, "Team", 1);
        if (awayId.HasValue) contest.AddParticipant(awayId.Value, "Team", 2);
        for (var quarter = 1; quarter <= 4; quarter++)
            contest.AddSegment($"Quarter {quarter}", "Quarter", quarter);
        contest.AddSegment(
            "Overtime",
            "Overtime",
            5,
            true,
            "tie_after_regulation");
        contest.Schedule(venue, startTime, 120);
        return contest;
    }

    private static void CompleteContest(Contest contest, Guid winnerId)
    {
        contest.Start();
        contest.SetWinner(winnerId);
        contest.Complete();
        contest.SubmitForApproval();
        contest.Approve();
    }

    private static void AddPublishedResults(
        ICollection<ContestResult> results,
        Guid contestId,
        int fixtureNumber,
        Guid homeId,
        int homeScore,
        Guid awayId,
        int awayScore,
        int firstResultNumber,
        int eventNumber)
    {
        results.Add(CreatePublishedResult(
            DemoId(firstResultNumber + 1),
            contestId,
            DemoId(eventNumber),
            homeId,
            homeScore,
            homeScore > awayScore ? 1 : 2,
            fixtureNumber));
        results.Add(CreatePublishedResult(
            DemoId(firstResultNumber + 2),
            contestId,
            DemoId(eventNumber),
            awayId,
            awayScore,
            awayScore > homeScore ? 1 : 2,
            fixtureNumber));
    }

    private static ContestResult CreatePublishedResult(
        Guid id,
        Guid contestId,
        Guid eventId,
        Guid participantId,
        int score,
        int rank,
        int fixtureNumber)
    {
        var result = new ContestResult(id, contestId, eventId, participantId);
        result.SetScore(score, score.ToString(), null);
        result.SetRank(rank, rank == 1 ? "1st" : "2nd");
        result.SetRemarks($"Deterministic basketball demo fixture {fixtureNumber}");
        result.Submit();
        result.BeginReview();
        result.Approve(SystemAdministratorId);
        result.Publish();
        return result;
    }

    private static void ApplyResult(TeamStats home, TeamStats away, int homeScore, int awayScore)
    {
        home.Played++;
        away.Played++;
        home.PointsFor += homeScore;
        home.PointsAgainst += awayScore;
        away.PointsFor += awayScore;
        away.PointsAgainst += homeScore;
        if (homeScore > awayScore)
        {
            home.Won++;
            away.Lost++;
        }
        else
        {
            away.Won++;
            home.Lost++;
        }
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"20000000-0000-0000-0000-{value:D12}");

    private sealed class TeamStats
    {
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int PointsFor { get; set; }
        public int PointsAgainst { get; set; }
    }
}
