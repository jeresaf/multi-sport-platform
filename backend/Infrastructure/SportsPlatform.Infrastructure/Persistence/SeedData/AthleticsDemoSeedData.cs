using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Deterministic National Sports sprint meet demonstrating heat and semifinal
/// qualification, disqualification, final ties, and medal projection.
/// </summary>
public static class AthleticsDemoSeedData
{
    public static readonly Guid CompetitionId = DemoId(100);

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

        var athletics = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "ATHLETICS", cancellationToken);
        if (athletics is null) return;
        var rulesetVersionId = athletics.Versions
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();

        var athleteIds = Enumerable.Range(0, 16)
            .Select(index => DemoId(200 + index))
            .ToArray();
        var existingAthletes = await context.Participants
            .OfType<Athlete>()
            .Where(item => athleteIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        for (var index = 0; index < athleteIds.Length; index++)
        {
            var athleteId = athleteIds[index];
            if (existingAthletes.ContainsKey(athleteId)) continue;
            var athlete = new Athlete(
                athleteId,
                DemoSeedData.NationalSportsTenantId,
                $"National 100m Sprinter {index + 1:D2}",
                $"NSP{index + 1:D2}");
            athlete.UpdatePersonalInfo(null, null, "Kenyan");
            athlete.RegisterForSport(athletics.Id);
            context.Participants.Add(athlete);
            existingAthletes.Add(athleteId, athlete);
        }

        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            CompetitionId,
            DemoSeedData.NationalSportsTenantId,
            "National Athletics 100m Demo",
            athletics.Id);
        if (rulesetVersionId.HasValue) competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();
        var edition = competition.AddEdition(
            "2031 National Sprint Meet",
            new DateTime(2031, 4, 12, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2031, 4, 12, 23, 59, 59, DateTimeKind.Utc));
        edition.Open();
        var stage = edition.AddStage("Men's 100m", StageType.Heat, 1);
        stage.Open();
        context.Competitions.Add(competition);

        var heat1 = CreateRace(
            1001, athletics.Id, rulesetVersionId, stage.Id, 1, "Heat", "Men's 100m — Heat 1",
            new DateTime(2031, 4, 12, 9, 0, 0, DateTimeKind.Utc),
            athleteIds.Take(8).Select((id, index) => (id, index + 1)),
            qualifyByPlace: 3, qualifyByTime: 2, nextRoundHeatCount: 2,
            nextRoundQualifyByPlace: 2, nextRoundQualifyByTime: 2);
        var heat2 = CreateRace(
            1002, athletics.Id, rulesetVersionId, stage.Id, 2, "Heat", "Men's 100m — Heat 2",
            new DateTime(2031, 4, 12, 9, 15, 0, DateTimeKind.Utc),
            athleteIds.Skip(8).Take(8).Select((id, index) => (id, index + 1)),
            qualifyByPlace: 3, qualifyByTime: 2, nextRoundHeatCount: 2,
            nextRoundQualifyByPlace: 2, nextRoundQualifyByTime: 2);

        ApplyRaceResults(
            context,
            heat1,
            new[] { 10_000, 10_200, 10_400, 0, 10_700, 10_900, 11_100, 11_300 },
            rulesetVersionId,
            disqualifiedIndex: 3);
        ApplyRaceResults(
            context,
            heat2,
            new[] { 10_100, 10_300, 10_500, 10_600, 10_800, 11_000, 11_200, 11_400 },
            rulesetVersionId);

        SetQualification(heat1, athleteIds[0], "Qualified", "Qualified by place (1) in heat 1");
        SetQualification(heat1, athleteIds[1], "Qualified", "Qualified by place (2) in heat 1");
        SetQualification(heat1, athleteIds[2], "Qualified", "Qualified by place (3) in heat 1");
        SetQualification(heat1, athleteIds[4], "Qualified", "Qualified by time (fastest loser)");
        SetQualification(heat1, athleteIds[5], "Eliminated", "Did not qualify");
        SetQualification(heat1, athleteIds[6], "Eliminated", "Did not qualify");
        SetQualification(heat1, athleteIds[7], "Eliminated", "Did not qualify");
        SetQualification(heat2, athleteIds[8], "Qualified", "Qualified by place (1) in heat 2");
        SetQualification(heat2, athleteIds[9], "Qualified", "Qualified by place (2) in heat 2");
        SetQualification(heat2, athleteIds[10], "Qualified", "Qualified by place (3) in heat 2");
        SetQualification(heat2, athleteIds[11], "Qualified", "Qualified by time (fastest loser)");
        for (var index = 12; index < 16; index++)
            SetQualification(heat2, athleteIds[index], "Eliminated", "Did not qualify");
        heat1.MarkQualificationApplied();
        heat2.MarkQualificationApplied();

        var semifinal1 = CreateRace(
            1003, athletics.Id, rulesetVersionId, stage.Id, 3, "AthleticsSemifinal",
            "Men's 100m — Semifinal 1",
            new DateTime(2031, 4, 12, 11, 0, 0, DateTimeKind.Utc),
            new[]
            {
                (athleteIds[0], 4), (athleteIds[9], 5),
                (athleteIds[10], 3), (athleteIds[4], 6)
            },
            qualifyByPlace: 2, qualifyByTime: 2);
        var semifinal2 = CreateRace(
            1004, athletics.Id, rulesetVersionId, stage.Id, 4, "AthleticsSemifinal",
            "Men's 100m — Semifinal 2",
            new DateTime(2031, 4, 12, 11, 15, 0, DateTimeKind.Utc),
            new[]
            {
                (athleteIds[8], 4), (athleteIds[1], 5),
                (athleteIds[2], 3), (athleteIds[11], 6)
            },
            qualifyByPlace: 2, qualifyByTime: 2);
        ApplyRaceResults(
            context, semifinal1, new[] { 10_010, 10_210, 10_410, 10_610 }, rulesetVersionId);
        ApplyRaceResults(
            context, semifinal2, new[] { 10_020, 10_220, 10_420, 10_620 }, rulesetVersionId);

        SetQualification(semifinal1, athleteIds[0], "Qualified", "Qualified by place (1) in semifinal 1");
        SetQualification(semifinal1, athleteIds[9], "Qualified", "Qualified by place (2) in semifinal 1");
        SetQualification(semifinal1, athleteIds[10], "Qualified", "Qualified by time (fastest loser)");
        SetQualification(semifinal1, athleteIds[4], "Eliminated", "Did not qualify");
        SetQualification(semifinal2, athleteIds[8], "Qualified", "Qualified by place (1) in semifinal 2");
        SetQualification(semifinal2, athleteIds[1], "Qualified", "Qualified by place (2) in semifinal 2");
        SetQualification(semifinal2, athleteIds[2], "Qualified", "Qualified by time (fastest loser)");
        SetQualification(semifinal2, athleteIds[11], "Eliminated", "Did not qualify");
        semifinal1.MarkQualificationApplied();
        semifinal2.MarkQualificationApplied();

        var final = CreateRace(
            1005, athletics.Id, rulesetVersionId, stage.Id, 5, "AthleticsFinal",
            "Men's 100m — Final",
            new DateTime(2031, 4, 12, 14, 0, 0, DateTimeKind.Utc),
            new[]
            {
                (athleteIds[0], 4), (athleteIds[8], 5), (athleteIds[9], 3),
                (athleteIds[1], 6), (athleteIds[10], 2), (athleteIds[2], 7)
            });
        ApplyRaceResults(
            context,
            final,
            new[] { 9_800, 9_900, 10_000, 10_100, 10_100, 10_300 },
            rulesetVersionId);
        final.SetWinner(athleteIds[0]);

        context.Contests.AddRange(heat1, heat2, semifinal1, semifinal2, final);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Contest CreateRace(
        int id,
        Guid sportId,
        Guid? rulesetVersionId,
        Guid stageId,
        int sortOrder,
        string contestShape,
        string name,
        DateTime startTime,
        IEnumerable<(Guid ParticipantId, int Lane)> participants,
        int qualifyByPlace = 0,
        int qualifyByTime = 0,
        int nextRoundHeatCount = 0,
        int nextRoundQualifyByPlace = 0,
        int nextRoundQualifyByTime = 0)
    {
        var race = new Contest(
            DemoId(id),
            DemoSeedData.NationalSportsTenantId,
            sportId,
            contestShape);
        race.SetName(name);
        race.AssignToStage(stageId, sortOrder);
        if (rulesetVersionId.HasValue) race.AssignRulesetVersion(rulesetVersionId.Value);
        race.ConfigureAthleticsRace(
            "Men's 100m",
            100,
            qualifyByPlace,
            qualifyByTime,
            advanceTies: true,
            lanesPerHeat: 8,
            laneAssignment: "Seeded",
            nextRoundHeatCount,
            nextRoundQualifyByPlace,
            nextRoundQualifyByTime,
            advancePlaceTies: false,
            advanceTimeTies: true);
        foreach (var participant in participants)
            race.AddParticipant(participant.ParticipantId, "Athlete", participant.Lane);
        race.AddSegment("Race", "Race", 1);
        race.Schedule("National Athletics Stadium", startTime, 15);
        return race;
    }

    private static void ApplyRaceResults(
        SportsPlatformDbContext context,
        Contest race,
        IReadOnlyList<int> times,
        Guid? rulesetVersionId,
        int? disqualifiedIndex = null)
    {
        var participants = race.Participants.ToArray();
        if (times.Count != participants.Length)
            throw new InvalidOperationException("Every race participant requires a demo result.");
        var segment = race.Segments.Single();
        race.Start();
        segment.Start();
        var sequence = 0;
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            if (index == disqualifiedIndex)
            {
                participant.Disqualify();
                participant.SetQualification("Disqualified", "False start");
                continue;
            }

            var time = times[index];
            var score = new LiveScore(
                DemoId(2000 + ((race.SortOrder - 1) * 20) + index + 1),
                race.Id,
                DemoId(3000 + ((race.SortOrder - 1) * 20) + index + 1),
                participant.ParticipantId,
                "finish_time_ms",
                time,
                segment.Id,
                tenantId: DemoSeedData.NationalSportsTenantId,
                eventType: "FinishTime",
                deviceTimestamp: race.StartTime!.Value.AddSeconds(time / 1000d),
                deviceId: "athletics-demo-seed",
                idempotencyKey: $"athletics-demo-race-{race.SortOrder:D2}-lane-{participant.SortOrder:D2}",
                rulesetVersionId: rulesetVersionId);
            score.SetSequence(++sequence);
            score.SetDisplayValue($"{time / 1000}.{time % 1000:D3}s");
            score.SetScorer("athletics-demo-seed", null);
            context.LiveScores.Add(score);
        }
        segment.End();
        race.Complete();
    }

    private static void SetQualification(
        Contest race,
        Guid participantId,
        string status,
        string reason) =>
        race.Participants.Single(item => item.ParticipantId == participantId)
            .SetQualification(status, reason);

    private static Guid DemoId(int value) =>
        Guid.Parse($"50000000-0000-0000-0000-{value:D12}");
}
