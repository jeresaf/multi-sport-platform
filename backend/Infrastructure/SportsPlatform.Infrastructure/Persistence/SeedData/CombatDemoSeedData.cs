using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Deterministic National Sports eight-bout combat card demonstrating stoppage,
/// submission, and judge-decision result paths.
/// </summary>
public static class CombatDemoSeedData
{
    public static readonly Guid CompetitionId = DemoId(100);

    private static readonly string[] AllowedResultMethods =
    {
        "Unanimous Decision",
        "Knockout",
        "Submission"
    };

    private static readonly string[] WeightClasses =
    {
        "Flyweight",
        "Bantamweight",
        "Featherweight",
        "Lightweight",
        "Welterweight",
        "Middleweight",
        "Light Heavyweight",
        "Heavyweight"
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

        var combat = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "MMA", cancellationToken);
        if (combat is null) return;
        var rulesetVersionId = combat.Versions
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();

        var fighterIds = Enumerable.Range(0, 16)
            .Select(index => DemoId(200 + index))
            .ToArray();
        var existingFighters = await context.Participants
            .OfType<Athlete>()
            .Where(item => fighterIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        for (var index = 0; index < fighterIds.Length; index++)
        {
            var fighterId = fighterIds[index];
            if (existingFighters.ContainsKey(fighterId)) continue;
            var fighter = new Athlete(
                fighterId,
                DemoSeedData.NationalSportsTenantId,
                $"National Combat Fighter {index + 1:D2}",
                $"NCF{index + 1:D2}");
            fighter.UpdatePersonalInfo(null, null, "Kenyan");
            fighter.RegisterForSport(combat.Id);
            context.Participants.Add(fighter);
            existingFighters.Add(fighterId, fighter);
        }

        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            CompetitionId,
            DemoSeedData.NationalSportsTenantId,
            "National Combat Night Demo",
            combat.Id);
        if (rulesetVersionId.HasValue) competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();
        var edition = competition.AddEdition(
            "2031 Combat Night",
            new DateTime(2031, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2031, 2, 16, 0, 0, 0, DateTimeKind.Utc));
        edition.Open();
        var stage = edition.AddStage("Fight Card", StageType.FightCard, 1);
        stage.Open();
        context.Competitions.Add(competition);

        var cardStart = new DateTime(2031, 2, 15, 15, 0, 0, DateTimeKind.Utc);
        for (var boutIndex = 0; boutIndex < 8; boutIndex++)
        {
            var fighter1Id = fighterIds[boutIndex * 2];
            var fighter2Id = fighterIds[(boutIndex * 2) + 1];
            var scheduledRounds = boutIndex == 7 ? 5 : 3;
            var cardSection = boutIndex switch
            {
                < 4 => "PreliminaryCard",
                < 6 => "MainCard",
                6 => "CoMainEvent",
                _ => "MainEvent"
            };
            var bout = new Contest(
                DemoId(1001 + boutIndex),
                DemoSeedData.NationalSportsTenantId,
                combat.Id,
                "Bout");
            bout.AssignToStage(stage.Id, boutIndex + 1);
            if (rulesetVersionId.HasValue) bout.AssignRulesetVersion(rulesetVersionId.Value);
            bout.SetName(
                $"Bout {boutIndex + 1}: National Combat Fighter {(boutIndex * 2) + 1:D2} vs "
                + $"National Combat Fighter {(boutIndex * 2) + 2:D2}");
            bout.ConfigureBout(
                WeightClasses[boutIndex],
                cardSection,
                scheduledRounds,
                AllowedResultMethods);
            bout.AddParticipant(fighter1Id, "Athlete", 1);
            bout.AddParticipant(fighter2Id, "Athlete", 2);

            var refereeId = DemoId(5000 + (boutIndex * 10));
            bout.AddOfficial(
                refereeId,
                "Referee",
                $"National Referee {boutIndex + 1}");
            var judgeIds = new Guid[3];
            for (var judgeIndex = 0; judgeIndex < judgeIds.Length; judgeIndex++)
            {
                judgeIds[judgeIndex] = DemoId(5001 + (boutIndex * 10) + judgeIndex);
                bout.AddOfficial(
                    judgeIds[judgeIndex],
                    "Judge",
                    $"National Judge {boutIndex + 1}-{judgeIndex + 1}");
            }

            var rounds = new List<ContestSegment>();
            for (var roundNumber = 1; roundNumber <= scheduledRounds; roundNumber++)
                rounds.Add(bout.AddSegment($"Round {roundNumber}", "Round", roundNumber));
            bout.Schedule(
                "National Combat Arena",
                cardStart.AddMinutes(boutIndex * 35),
                30);
            bout.Start();

            var method = (boutIndex % 3) switch
            {
                0 => "Unanimous Decision",
                1 => "Knockout",
                _ => "Submission"
            };
            var winnerId = method == "Submission" ? fighter2Id : fighter1Id;
            var completedRounds = method == "Unanimous Decision" ? scheduledRounds : 2;
            for (var roundIndex = 0; roundIndex < completedRounds; roundIndex++)
            {
                rounds[roundIndex].Start();
                rounds[roundIndex].End();
            }

            if (method == "Unanimous Decision")
            {
                for (var judgeIndex = 0; judgeIndex < judgeIds.Length; judgeIndex++)
                {
                    var scorecard = new
                    {
                        OfficialId = judgeIds[judgeIndex],
                        Rounds = Enumerable.Range(1, scheduledRounds).Select(roundNumber => new
                        {
                            RoundNumber = roundNumber,
                            Fighter1Score = 10,
                            Fighter2Score = 9
                        }).ToArray()
                    };
                    var scoreEvent = bout.AddEvent(
                        "JudgeScorecard",
                        fighter1Id,
                        null,
                        JsonSerializer.Serialize(scorecard),
                        judgeIds[judgeIndex],
                        "combat-demo-seed",
                        judgeIndex + 1,
                        $"combat-demo-{boutIndex + 1:D2}-judge-{judgeIndex + 1}");
                    scoreEvent.Approve();
                }
            }

            bout.RecordBoutResult(
                winnerId,
                method,
                method == "Unanimous Decision" ? scheduledRounds : 2,
                method == "Unanimous Decision" ? null : 145,
                "Official deterministic combat demo result");
            bout.Complete();
            context.Contests.Add(bout);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"40000000-0000-0000-0000-{value:D12}");
}
