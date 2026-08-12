using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Deterministic National Sports four-team T20 tournament demonstrating the
/// complete group, semifinal, final, scorecard, and champion flow.
/// </summary>
public static class CricketDemoSeedData
{
    public static readonly Guid CompetitionId = DemoId(100);

    private static readonly (string Name, string ShortName)[] TeamDefinitions =
    {
        ("Nairobi Strikers", "Strikers"),
        ("Coast Chargers", "Chargers"),
        ("Rift Valley Rhinos", "Rhinos"),
        ("Lake Victoria Kings", "Kings")
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

        var cricket = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "CRICKET", cancellationToken);
        if (cricket is null) return;
        var rulesetVersionId = cricket.Versions
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
        for (var index = 0; index < teamIds.Length; index++)
        {
            var teamId = teamIds[index];
            if (existingTeams.ContainsKey(teamId)) continue;
            var definition = TeamDefinitions[index];
            var team = new Team(
                teamId,
                DemoSeedData.NationalSportsTenantId,
                definition.Name,
                definition.ShortName);
            team.UpdateDetails(
                "National Cricket Ground",
                $"Demo Cricket Coach {index + 1}",
                $"Demo Cricket Manager {index + 1}");
            context.Participants.Add(team);
            existingTeams.Add(teamId, team);
        }

        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            CompetitionId,
            DemoSeedData.NationalSportsTenantId,
            "National T20 Championship Demo",
            cricket.Id);
        if (rulesetVersionId.HasValue) competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();
        var edition = competition.AddEdition(
            "2031 National T20 Cup",
            new DateTime(2031, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2031, 7, 12, 23, 59, 59, DateTimeKind.Utc));
        edition.Open();
        var groupStage = edition.AddStage("T20 Group Stage", StageType.Group, 1);
        groupStage.Open();
        var knockoutStage = edition.AddStage("T20 Knockout", StageType.Knockout, 2);
        knockoutStage.Open();
        context.Competitions.Add(competition);

        var groupPairings = new[]
        {
            (First: 0, Second: 1),
            (First: 0, Second: 2),
            (First: 0, Second: 3),
            (First: 1, Second: 2),
            (First: 1, Second: 3),
            (First: 2, Second: 3)
        };
        var contests = new List<Contest>();
        for (var index = 0; index < groupPairings.Length; index++)
        {
            var pairing = groupPairings[index];
            contests.Add(CreateCompletedMatch(
                1001 + index,
                cricket.Id,
                rulesetVersionId,
                groupStage.Id,
                "CricketGroupMatch",
                index + 1,
                teamIds[pairing.First],
                teamIds[pairing.Second],
                $"{TeamDefinitions[pairing.First].Name} vs {TeamDefinitions[pairing.Second].Name}",
                new DateTime(2031, 7, 1, 9, 0, 0, DateTimeKind.Utc).AddDays(index)));
        }

        var semifinal1 = CreateCompletedMatch(
            1007,
            cricket.Id,
            rulesetVersionId,
            knockoutStage.Id,
            "CricketSemifinal",
            1,
            teamIds[0],
            teamIds[3],
            $"Semifinal 1: {TeamDefinitions[0].Name} vs {TeamDefinitions[3].Name}",
            new DateTime(2031, 7, 9, 9, 0, 0, DateTimeKind.Utc));
        var semifinal2 = CreateCompletedMatch(
            1008,
            cricket.Id,
            rulesetVersionId,
            knockoutStage.Id,
            "CricketSemifinal",
            2,
            teamIds[1],
            teamIds[2],
            $"Semifinal 2: {TeamDefinitions[1].Name} vs {TeamDefinitions[2].Name}",
            new DateTime(2031, 7, 9, 14, 0, 0, DateTimeKind.Utc));
        var final = CreateCompletedMatch(
            1009,
            cricket.Id,
            rulesetVersionId,
            knockoutStage.Id,
            "CricketFinal",
            3,
            teamIds[0],
            teamIds[1],
            $"T20 Final: {TeamDefinitions[0].Name} vs {TeamDefinitions[1].Name}",
            new DateTime(2031, 7, 11, 14, 0, 0, DateTimeKind.Utc));
        contests.AddRange(new[] { semifinal1, semifinal2, final });

        context.Contests.AddRange(contests);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Contest CreateCompletedMatch(
        int id,
        Guid sportId,
        Guid? rulesetVersionId,
        Guid stageId,
        string shape,
        int sortOrder,
        Guid firstTeamId,
        Guid secondTeamId,
        string name,
        DateTime startTime)
    {
        var match = new Contest(
            DemoId(id),
            DemoSeedData.NationalSportsTenantId,
            sportId,
            shape);
        match.AssignToStage(stageId, sortOrder);
        if (rulesetVersionId.HasValue) match.AssignRulesetVersion(rulesetVersionId.Value);
        match.AddParticipant(firstTeamId, "Team", 1);
        match.AddParticipant(secondTeamId, "Team", 2);
        match.SetName(name);

        var inningsSegments = new List<ContestSegment>();
        for (var inningsNumber = 1; inningsNumber <= 2; inningsNumber++)
        {
            var innings = match.AddSegment(
                $"Innings {inningsNumber}",
                "Innings",
                inningsNumber);
            for (var over = 1; over <= 20; over++)
                innings.AddNestedSegment($"Over {over}", "Over", over);
            inningsSegments.Add(innings);
        }
        match.Schedule("National Cricket Ground", startTime, 210);
        match.Start();

        AddInnings(match, inningsSegments[0], 1, firstTeamId, 120, 10, 20);
        AddInnings(match, inningsSegments[1], 2, secondTeamId, 100, 10, 20);
        match.SetWinner(firstTeamId);
        match.Complete();
        return match;
    }

    private static void AddInnings(
        Contest match,
        ContestSegment innings,
        int inningsNumber,
        Guid battingTeamId,
        int totalRuns,
        int wickets,
        int balls)
    {
        innings.Start();
        var remainingRuns = totalRuns;
        var sequence = match.Events.Select(item => item.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max();
        for (var index = 0; index < balls; index++)
        {
            var overNumber = (index / 6) + 1;
            var ballNumber = (index % 6) + 1;
            var runs = Math.Min(6, remainingRuns);
            remainingRuns -= runs;
            var isWicket = index >= balls - wickets;
            var over = innings.NestedSegments.Single(item => item.SortOrder == overNumber);
            if (over.Status == SegmentStatus.Pending) over.Start();
            var payload = new
            {
                InningsNumber = inningsNumber,
                OverNumber = overNumber,
                BallNumber = ballNumber,
                Runs = runs,
                ExtraRuns = 0,
                ExtraType = (string?)null,
                IsWicket = isWicket
            };
            var delivery = match.AddEvent(
                "CricketDelivery",
                battingTeamId,
                over.Id,
                JsonSerializer.Serialize(payload),
                DemoId(9000),
                "cricket-demo-seed",
                ++sequence,
                $"cricket-demo-{match.Id:N}-innings-{inningsNumber}-ball-{index + 1:D3}");
            delivery.Approve();
            if (ballNumber == 6 || index == balls - 1) over.End();
        }

        if (remainingRuns != 0)
            throw new InvalidOperationException("The deterministic innings cannot represent its run total.");
        var summary = new
        {
            InningsNumber = inningsNumber,
            BattingTeamId = battingTeamId,
            Runs = totalRuns,
            Wickets = wickets,
            Balls = balls,
            Extras = 0
        };
        var summaryEvent = match.AddEvent(
            "CricketInningsSummary",
            battingTeamId,
            innings.Id,
            JsonSerializer.Serialize(summary),
            DemoId(9000),
            "cricket-demo-seed",
            ++sequence,
            $"cricket-demo-{match.Id:N}-innings-{inningsNumber}-summary");
        summaryEvent.Approve();
        innings.End();
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"60000000-0000-0000-0000-{value:D12}");
}
