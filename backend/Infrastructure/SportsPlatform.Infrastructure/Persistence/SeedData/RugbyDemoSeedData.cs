using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Standings.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Upgrade-safe Rugby Sevens flagship tournament. The legacy Six Nations seed
/// remains the Fifteens showcase; this package proves the shorter format from
/// ruleset selection through pools, knockout matches, scores and standings.
/// </summary>
public static class RugbyDemoSeedData
{
    public static readonly Guid SevensCompetitionId = DemoId(100);
    private static readonly Guid SystemAdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    private static readonly (string Name, string ShortName)[] Teams =
    {
        ("Kenya Sevens", "Shujaa"),
        ("South Africa Sevens", "Blitzboks"),
        ("New Zealand Sevens", "All Blacks 7s"),
        ("Fiji Sevens", "Fiji 7s"),
        ("Australia Sevens", "Australia 7s"),
        ("Argentina Sevens", "Los Pumas 7s"),
        ("France Sevens", "France 7s"),
        ("Great Britain Sevens", "GB 7s")
    };

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        var rugby = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "RUGBY", cancellationToken);
        if (rugby is null) return;
        var fifteensRulesetId = rugby.Versions
            .Where(item => item.IsPublished
                && RugbyVariants.FromValidationConfiguration(item.ValidationConfiguration)
                    == RugbyVariants.Fifteens)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();
        if (fifteensRulesetId.HasValue)
        {
            var sixNationsId = Guid.Parse("00000000-0000-0000-0000-000000000306");
            var sixNations = await context.Competitions
                .Include(item => item.Editions)
                .ThenInclude(item => item.Stages)
                .SingleOrDefaultAsync(item => item.Id == sixNationsId, cancellationToken);
            if (sixNations is not null && !sixNations.RulesetVersionId.HasValue)
            {
                sixNations.AssignRulesetVersion(fifteensRulesetId.Value);
                var stageIds = sixNations.Editions.SelectMany(item => item.Stages)
                    .Select(item => item.Id).ToArray();
                var legacyMatches = await context.Contests
                    .Where(item => item.StageId.HasValue
                        && stageIds.Contains(item.StageId.Value)
                        && !item.RulesetVersionId.HasValue)
                    .ToArrayAsync(cancellationToken);
                foreach (var match in legacyMatches)
                    match.AssignRulesetVersion(fifteensRulesetId.Value);
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        if (await context.Competitions.AnyAsync(
                item => item.Id == SevensCompetitionId,
                cancellationToken))
            return;

        var rulesetVersionId = rugby.Versions
            .Where(item => item.IsPublished
                && RugbyVariants.FromValidationConfiguration(item.ValidationConfiguration)
                    == RugbyVariants.Sevens)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();
        if (!rulesetVersionId.HasValue) return;

        var teamIds = Enumerable.Range(0, Teams.Length)
            .Select(index => DemoId(200 + index))
            .ToArray();
        var existing = await context.Participants.OfType<Team>()
            .Where(item => teamIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        for (var index = 0; index < Teams.Length; index++)
        {
            if (existing.Contains(teamIds[index])) continue;
            var team = new Team(
                teamIds[index],
                DemoSeedData.NationalSportsTenantId,
                Teams[index].Name,
                Teams[index].ShortName);
            team.UpdateDetails(
                "National Rugby Sevens Centre",
                $"Sevens Coach {index + 1}",
                $"Sevens Manager {index + 1}");
            context.Participants.Add(team);
        }
        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            SevensCompetitionId,
            DemoSeedData.NationalSportsTenantId,
            "National Rugby Sevens Flagship Demo",
            rugby.Id);
        competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();
        var edition = competition.AddEdition(
            "2031 Sevens Series Final",
            new DateTime(2031, 8, 16, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2031, 8, 17, 23, 59, 59, DateTimeKind.Utc));
        edition.Open();
        var poolStage = edition.AddStage("Pool Stage", StageType.Group, 1);
        poolStage.Open();
        var poolA = poolStage.AddGroup("Pool A");
        var poolB = poolStage.AddGroup("Pool B");
        var knockoutStage = edition.AddStage("Cup Knockout", StageType.Knockout, 2);
        knockoutStage.Open();
        context.Competitions.Add(competition);

        var poolScores = new (int Home, int Away)[]
        {
            (24, 17), (19, 12), (26, 7), (21, 14), (15, 10), (28, 19),
            (22, 17), (12, 10), (19, 14), (26, 21), (17, 15), (24, 12)
        };
        var poolTeamIndexes = new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 } };
        var allContests = new List<Contest>();
        var allResults = new List<ContestResult>();
        var allScores = new List<LiveScore>();
        var standings = new Dictionary<(Guid GroupId, Guid TeamId), RugbyStats>();
        var fixture = 0;
        foreach (var (indexes, group) in new[] { (poolTeamIndexes[0], poolA), (poolTeamIndexes[1], poolB) })
        {
            for (var first = 0; first < indexes.Length - 1; first++)
            for (var second = first + 1; second < indexes.Length; second++)
            {
                var homeId = teamIds[indexes[first]];
                var awayId = teamIds[indexes[second]];
                var score = poolScores[fixture++];
                var contest = CreateCompletedMatch(
                    1000 + fixture, rugby.Id, rulesetVersionId.Value, poolStage.Id,
                    group.Id, fixture, homeId, awayId, score.Home, score.Away,
                    $"{Teams[indexes[first]].Name} vs {Teams[indexes[second]].Name}",
                    new DateTime(2031, 8, 16, 8, 0, 0, DateTimeKind.Utc).AddMinutes((fixture - 1) * 35));
                AddOutcome(context, contest, homeId, awayId, score.Home, score.Away,
                    rulesetVersionId.Value, allResults, allScores);
                Apply(standings, group.Id, homeId, awayId, score.Home, score.Away);
                allContests.Add(contest);
            }
        }

        var rankedPools = standings.GroupBy(item => item.Key.GroupId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.Value.Points)
                    .ThenByDescending(item => item.Value.For - item.Value.Against)
                    .ThenByDescending(item => item.Value.For)
                    .Select(item => item.Key.TeamId)
                    .ToArray());
        var semi1 = CreateCompletedMatch(1101, rugby.Id, rulesetVersionId.Value,
            knockoutStage.Id, null, 1, rankedPools[poolA.Id][0], rankedPools[poolB.Id][1],
            24, 19, "Cup semifinal 1", new DateTime(2031, 8, 17, 11, 0, 0, DateTimeKind.Utc));
        var semi2 = CreateCompletedMatch(1102, rugby.Id, rulesetVersionId.Value,
            knockoutStage.Id, null, 2, rankedPools[poolB.Id][0], rankedPools[poolA.Id][1],
            21, 17, "Cup semifinal 2", new DateTime(2031, 8, 17, 11, 35, 0, DateTimeKind.Utc));
        var final = CreateCompletedMatch(1103, rugby.Id, rulesetVersionId.Value,
            knockoutStage.Id, null, 3, semi1.WinnerId!.Value, semi2.WinnerId!.Value,
            28, 24, "Rugby Sevens Cup Final", new DateTime(2031, 8, 17, 15, 30, 0, DateTimeKind.Utc));
        foreach (var match in new[] { semi1, semi2, final })
        {
            var participants = match.Participants.OrderBy(item => item.SortOrder).ToArray();
            var values = match == semi1 ? (24, 19) : match == semi2 ? (21, 17) : (28, 24);
            AddOutcome(context, match, participants[0].ParticipantId, participants[1].ParticipantId,
                values.Item1, values.Item2, rulesetVersionId.Value, allResults, allScores);
        }
        allContests.AddRange(new[] { semi1, semi2, final });
        context.Contests.AddRange(allContests);
        context.ContestResults.AddRange(allResults);
        context.LiveScores.AddRange(allScores);

        foreach (var pool in standings.GroupBy(item => item.Key.GroupId))
        {
            var ordered = pool.OrderByDescending(item => item.Value.Points)
                .ThenByDescending(item => item.Value.For - item.Value.Against)
                .ThenByDescending(item => item.Value.For)
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var stats = ordered[index].Value;
                var standing = new Standing(DemoId(3000 + context.Standings.Local.Count + 1),
                    competition.Id, pool.Key, ordered[index].Key.TeamId);
                standing.UpdateStats(stats.Played, stats.Won, stats.Drawn, stats.Lost,
                    stats.For, stats.Against, stats.Points);
                standing.SetPosition(index + 1);
                standing.SetMetadata(JsonSerializer.Serialize(new
                {
                    variant = RugbyVariants.Sevens,
                    tries = 0,
                    tryBonusPoints = 0,
                    losingBonusPoints = 0,
                    grandSlamBonusPoints = 0
                }));
                context.Standings.Add(standing);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Contest CreateCompletedMatch(int id, Guid sportId, Guid rulesetId,
        Guid stageId, Guid? groupId, int order, Guid homeId, Guid awayId,
        int homeScore, int awayScore, string name, DateTime start)
    {
        var contest = new Contest(DemoId(id), DemoSeedData.NationalSportsTenantId,
            sportId, "TeamVsTeam");
        contest.AssignRulesetVersion(rulesetId);
        contest.AssignToStage(stageId, order);
        if (groupId.HasValue) contest.AssignToGroup(groupId.Value);
        contest.AddParticipant(homeId, "Team", 1);
        contest.AddParticipant(awayId, "Team", 2);
        contest.SetName(name);
        contest.AddSegment("First Half", "Half", 1);
        contest.AddSegment("Second Half", "Half", 2);
        contest.AddSegment("Golden-point Extra Time", "ExtraTime", 3, true,
            "tie_after_regulation_in_knockout");
        contest.Schedule("National Rugby Sevens Centre", start, 25);
        contest.Start();
        contest.SetWinner(homeScore > awayScore ? homeId : awayId);
        contest.Complete();
        contest.SubmitForApproval();
        contest.Approve();
        return contest;
    }

    private static void AddOutcome(SportsPlatformDbContext context, Contest contest,
        Guid homeId, Guid awayId, int homeScore, int awayScore, Guid rulesetId,
        ICollection<ContestResult> results, ICollection<LiveScore> scores)
    {
        var eventBase = 5000 + scores.Count;
        foreach (var (teamId, value, rank, offset) in new[]
                 { (homeId, homeScore, homeScore > awayScore ? 1 : 2, 1),
                   (awayId, awayScore, awayScore > homeScore ? 1 : 2, 2) })
        {
            var eventId = DemoId(eventBase + offset);
            var score = new LiveScore(DemoId(6000 + scores.Count + 1), contest.Id,
                eventId, teamId, "score", value,
                tenantId: DemoSeedData.NationalSportsTenantId,
                eventType: "RUGBY_SCORE_SUMMARY", deviceId: "rugby-sevens-demo",
                idempotencyKey: $"rugby-sevens-{contest.Id:N}-{teamId:N}",
                rulesetVersionId: rulesetId);
            score.SetSequence(scores.Count + 1);
            score.SetDisplayValue(value.ToString());
            score.SetScorer("system-seed", null);
            scores.Add(score);

            var result = new ContestResult(DemoId(7000 + results.Count + 1),
                contest.Id, eventId, teamId);
            result.SetScore(value, value.ToString(), null);
            result.SetRank(rank, rank == 1 ? "1st" : "2nd");
            result.Submit();
            result.BeginReview();
            result.Approve(SystemAdministratorId);
            result.Publish();
            results.Add(result);
        }
    }

    private static void Apply(Dictionary<(Guid, Guid), RugbyStats> table, Guid groupId,
        Guid homeId, Guid awayId, int home, int away)
    {
        var homeStats = table.GetValueOrDefault((groupId, homeId)) ?? new RugbyStats();
        var awayStats = table.GetValueOrDefault((groupId, awayId)) ?? new RugbyStats();
        homeStats.Played++; awayStats.Played++;
        homeStats.For += home; homeStats.Against += away;
        awayStats.For += away; awayStats.Against += home;
        if (home == away)
        {
            homeStats.Drawn++; awayStats.Drawn++; homeStats.Points += 2; awayStats.Points += 2;
        }
        else
        {
            var winner = home > away ? homeStats : awayStats;
            var loser = home > away ? awayStats : homeStats;
            winner.Won++; winner.Points += 3; loser.Lost++; loser.Points += 1;
        }
        table[(groupId, homeId)] = homeStats;
        table[(groupId, awayId)] = awayStats;
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"70000000-0000-0000-0000-{value:D12}");

    private sealed class RugbyStats
    {
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int For { get; set; }
        public int Against { get; set; }
        public decimal Points { get; set; }
    }
}
