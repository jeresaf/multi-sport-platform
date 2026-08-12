using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Sports.Services;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Deterministic Short Tournament golf demo with an inclusive halfway cut and
/// a final round that remains open for normal scorecard entry.
/// </summary>
public static class GolfDemoSeedData
{
    public static readonly Guid CompetitionId = DemoId(100);
    public static readonly Guid ContestId = DemoId(1000);

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

        var golf = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "GOLF", cancellationToken);
        if (golf is null) return;

        var rulesetVersionId = golf.Versions
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();
        var golferIds = Enumerable.Range(0, 24)
            .Select(index => DemoId(200 + index))
            .ToArray();
        var existingGolfers = await context.Participants
            .OfType<Athlete>()
            .Where(item => golferIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        for (var index = 0; index < golferIds.Length; index++)
        {
            var golferId = golferIds[index];
            if (existingGolfers.ContainsKey(golferId)) continue;
            var golfer = new Athlete(
                golferId,
                DemoSeedData.ShortTournamentTenantId,
                $"Short Tour Golfer {index + 1:D2}",
                $"STG{index + 1:D2}");
            golfer.UpdatePersonalInfo(
                new DateTime(
                    1990 + (index % 12),
                    1 + (index % 12),
                    1 + (index % 27),
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                null,
                "Kenyan");
            golfer.RegisterForSport(golf.Id);
            context.Participants.Add(golfer);
            existingGolfers.Add(golferId, golfer);
        }

        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            CompetitionId,
            DemoSeedData.ShortTournamentTenantId,
            "Short Tournament Golf Open Demo",
            golf.Id);
        if (rulesetVersionId.HasValue) competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();
        var edition = competition.AddEdition(
            "2030 Demo Open",
            new DateTime(2030, 11, 7, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 11, 10, 0, 0, 0, DateTimeKind.Utc));
        edition.Open();
        var stage = edition.AddStage("Four-Round Stroke Play", StageType.Leaderboard, 1);
        stage.Open();
        context.Competitions.Add(competition);

        var contest = new Contest(
            ContestId,
            DemoSeedData.ShortTournamentTenantId,
            golf.Id,
            "MultiRoundLeaderboard");
        contest.SetName("Short Tournament Golf Open");
        if (rulesetVersionId.HasValue) contest.AssignRulesetVersion(rulesetVersionId.Value);
        contest.AssignToStage(stage.Id, 1);
        contest.ConfigureLeaderboard(
            72,
            2,
            12,
            "Savannah Hills Golf Club",
            "Nairobi, Kenya",
            GolfRulesetService.StandardHolePars);

        var contestParticipants = new List<ContestParticipant>();
        foreach (var golferId in golferIds)
            contestParticipants.Add(contest.AddParticipant(golferId, "Athlete"));

        var rounds = new List<ContestSegment>();
        for (var roundNumber = 1; roundNumber <= 4; roundNumber++)
        {
            var round = contest.AddSegment($"Round {roundNumber}", "Round", roundNumber);
            for (var holeNumber = 1; holeNumber <= 18; holeNumber++)
            {
                var par = GolfRulesetService.StandardHolePars[holeNumber - 1];
                round.AddNestedSegment(
                    $"Hole {holeNumber} (Par {par})",
                    "Hole",
                    holeNumber);
            }
            rounds.Add(round);
        }

        contest.Schedule(
            "Savannah Hills Golf Club",
            new DateTime(2030, 11, 7, 6, 30, 0, DateTimeKind.Utc),
            720);
        contest.Start();

        // The requested top-12 cut includes a tie at the line, so 13 advance.
        for (var index = 0; index < contestParticipants.Count; index++)
            contestParticipants[index].SetCutStatus(index <= 12);
        contest.MarkCutApplied();
        for (var index = 0; index < rounds.Count; index++)
        {
            rounds[index].Start();
            if (index < 3) rounds[index].End();
        }
        context.Contests.Add(contest);

        var sequence = 0;
        var liveScores = new List<LiveScore>();
        for (var golferIndex = 0; golferIndex < golferIds.Length; golferIndex++)
        {
            var roundsToSeed = golferIndex <= 12 ? 3 : 2;
            var modifier = golferIndex == 12 ? 11 : golferIndex;
            for (var roundNumber = 1; roundNumber <= roundsToSeed; roundNumber++)
            {
                var holeScores = BuildHoleScores(modifier);
                for (var holeIndex = 0; holeIndex < holeScores.Length; holeIndex++)
                {
                    sequence++;
                    var scoredAt = new DateTime(
                            2030, 11, 6 + roundNumber, 7, 0, 0, DateTimeKind.Utc)
                        .AddMinutes((golferIndex * 8) + (holeIndex * 12));
                    var score = new LiveScore(
                        DemoId(10000 + sequence),
                        ContestId,
                        DemoId(20000 + sequence),
                        golferIds[golferIndex],
                        "strokes",
                        holeScores[holeIndex],
                        rounds[roundNumber - 1].NestedSegments.ElementAt(holeIndex).Id,
                        tenantId: DemoSeedData.ShortTournamentTenantId,
                        eventType: "HOLE_SCORE",
                        deviceTimestamp: scoredAt,
                        deviceId: "golf-demo-seed",
                        idempotencyKey:
                            $"golf-demo-{golferIndex + 1:D2}-r{roundNumber}-h{holeIndex + 1:D2}",
                        rulesetVersionId: rulesetVersionId);
                    score.SetSequence(sequence);
                    score.SetDisplayValue(holeScores[holeIndex].ToString());
                    score.SetScorer("system-seed", null);
                    liveScores.Add(score);
                }
            }
        }

        context.LiveScores.AddRange(liveScores);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static int[] BuildHoleScores(int modifier)
    {
        var scores = GolfRulesetService.StandardHolePars.ToArray();
        for (var increment = 0; increment < modifier; increment++)
            scores[increment % scores.Length]++;
        return scores;
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"30000000-0000-0000-0000-{value:D12}");
}
