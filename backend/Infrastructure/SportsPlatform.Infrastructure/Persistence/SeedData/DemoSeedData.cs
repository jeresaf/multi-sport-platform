using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Branding.Entities;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Upgrade-safe, deterministic demo data required by the delivery task list.
/// Fixed aggregate IDs make repeated startup seeding idempotent on existing databases.
/// </summary>
public static class DemoSeedData
{
    public static readonly Guid NationalSportsTenantId = DemoId(1);
    public static readonly Guid SchoolGamesTenantId = DemoId(2);
    public static readonly Guid ShortTournamentTenantId = DemoId(3);
    public static readonly Guid FootballCompetitionId = DemoId(100);
    public static readonly Guid FootballBracketId = DemoId(4000);

    private static readonly Guid SystemAdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    private static readonly (string Name, string ShortName, string Venue)[] FootballTeamDefinitions =
    {
        ("Nairobi City", "Nairobi", "City Stadium"),
        ("Mombasa United", "Mombasa", "Coastal Arena"),
        ("Kisumu Stars", "Kisumu", "Lakeside Stadium"),
        ("Nakuru Athletic", "Nakuru", "Rift Valley Arena"),
        ("Eldoret Rangers", "Eldoret", "Champions Ground"),
        ("Thika Lions", "Thika", "Thika Municipal Stadium"),
        ("Machakos FC", "Machakos", "Eastern Arena"),
        ("Nyeri Wanderers", "Nyeri", "Mount Kenya Ground")
    };

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        await EnsureDemoTenantsAsync(context, cancellationToken);
        await EnsureFootballDemoAsync(context, cancellationToken);
        await BasketballDemoSeedData.EnsureAsync(context, cancellationToken);
        await GolfDemoSeedData.EnsureAsync(context, cancellationToken);
        await CombatDemoSeedData.EnsureAsync(context, cancellationToken);
        await AthleticsDemoSeedData.EnsureAsync(context, cancellationToken);
        await CricketDemoSeedData.EnsureAsync(context, cancellationToken);
        await RugbyDemoSeedData.EnsureAsync(context, cancellationToken);
        await PublicE2eShowcaseSeedData.EnsureAsync(context, cancellationToken);
        await RegistrationDemoSeedData.EnsureAsync(context, cancellationToken);
    }

    private static async Task EnsureDemoTenantsAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new DemoTenantDefinition(
                NationalSportsTenantId, DemoId(11), "National Sports Demo", "NSD",
                "national-sports-demo", "A national multi-sport demonstration organisation.",
                "#102A43", "#F0B429", "#D64545", "demo-national@sportsplatform.local"),
            new DemoTenantDefinition(
                SchoolGamesTenantId, DemoId(12), "School Games Demo", "SGD",
                "school-games-demo", "An inter-school games demonstration organisation.",
                "#176B45", "#F28C28", "#F7C948", "demo-schools@sportsplatform.local"),
            new DemoTenantDefinition(
                ShortTournamentTenantId, DemoId(13), "Short Tournament Demo", "STD",
                "short-tournament-demo", "A compact tournament demonstration organisation.",
                "#5B2C83", "#10B8B8", "#F05D5E", "demo-tournament@sportsplatform.local")
        };

        var tenantIds = definitions.Select(item => item.TenantId).ToArray();
        var existingTenantIds = await context.Tenants
            .Where(item => tenantIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        foreach (var definition in definitions.Where(item => !existingTenantIds.Contains(item.TenantId)))
        {
            var tenant = new Tenant(
                definition.TenantId,
                definition.Name,
                definition.ShortName,
                definition.Slug);
            tenant.UpdateDetails(definition.Name, definition.ShortName, definition.Description);
            tenant.UpdateLocalization("Kenya", "Africa/Nairobi", "KES", "en");
            tenant.UpdateContact(definition.Email, null);
            context.Tenants.Add(tenant);
        }

        await context.SaveChangesAsync(cancellationToken);

        var existingBrandingTenantIds = await context.TenantBrandings
            .Where(item => tenantIds.Contains(item.TenantId))
            .Select(item => item.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var definition in definitions.Where(
                     item => !existingBrandingTenantIds.Contains(item.TenantId)))
        {
            var branding = new TenantBranding(
                definition.BrandingId,
                definition.TenantId,
                definition.Name);
            branding.UpdateBranding(
                definition.Name,
                definition.ShortName,
                null,
                definition.PrimaryColor,
                definition.SecondaryColor,
                definition.AccentColor);
            branding.UpdatePublicInfo(definition.Slug, definition.Email, null, null);
            context.TenantBrandings.Add(branding);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureFootballDemoAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Competitions.AnyAsync(
                item => item.Id == FootballCompetitionId,
                cancellationToken))
        {
            return;
        }

        var football = await context.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Code == "FOOTBALL", cancellationToken);
        if (football is null)
        {
            return;
        }

        var rulesetVersionId = football.Versions
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();

        var teamIds = Enumerable.Range(0, FootballTeamDefinitions.Length)
            .Select(index => DemoId(200 + index))
            .ToArray();
        var existingTeams = await context.Participants
            .OfType<Team>()
            .Where(item => teamIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        for (var index = 0; index < FootballTeamDefinitions.Length; index++)
        {
            var teamId = teamIds[index];
            if (existingTeams.ContainsKey(teamId)) continue;

            var definition = FootballTeamDefinitions[index];
            var team = new Team(teamId, NationalSportsTenantId, definition.Name, definition.ShortName);
            team.UpdateDetails(definition.Venue, $"Demo Coach {index + 1}", $"Demo Manager {index + 1}");
            context.Participants.Add(team);
            existingTeams.Add(teamId, team);
        }

        await context.SaveChangesAsync(cancellationToken);

        var competition = new Competition(
            FootballCompetitionId,
            NationalSportsTenantId,
            "National Football Championship Demo",
            football.Id);
        if (rulesetVersionId.HasValue) competition.AssignRulesetVersion(rulesetVersionId.Value);
        competition.Publish();

        var edition = competition.AddEdition(
            "2030 Demo Season",
            new DateTime(2030, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        edition.Open();
        var leagueStage = edition.AddStage("League Stage", StageType.League, 1);
        leagueStage.Open();
        var leagueGroup = leagueStage.AddGroup("National League Table");
        var knockoutStage = edition.AddStage("Championship Knockout", StageType.Knockout, 2);
        knockoutStage.Open();

        context.Competitions.Add(competition);

        var stats = teamIds.ToDictionary(item => item, _ => new TeamStats());
        var leagueContests = new List<Contest>();
        var results = new List<ContestResult>();
        var fixtureIndex = 0;

        for (var homeIndex = 0; homeIndex < teamIds.Length - 1; homeIndex++)
        {
            for (var awayIndex = homeIndex + 1; awayIndex < teamIds.Length; awayIndex++)
            {
                fixtureIndex++;
                var homeId = teamIds[homeIndex];
                var awayId = teamIds[awayIndex];
                var homeGoals = (homeIndex * 2 + awayIndex + 1) % 4;
                var awayGoals = (awayIndex * 3 + homeIndex) % 3;
                var contestId = DemoId(1000 + fixtureIndex);
                var contest = CreateFootballContest(
                    contestId,
                    football.Id,
                    rulesetVersionId,
                    leagueStage.Id,
                    leagueGroup.Id,
                    fixtureIndex,
                    $"{FootballTeamDefinitions[homeIndex].Name} vs {FootballTeamDefinitions[awayIndex].Name}",
                    FootballTeamDefinitions[homeIndex].Venue,
                    new DateTime(2030, 1, 5, 13, 0, 0, DateTimeKind.Utc).AddDays((fixtureIndex - 1) * 4),
                    homeId,
                    awayId);

                contest.Start();
                if (homeGoals != awayGoals) contest.SetWinner(homeGoals > awayGoals ? homeId : awayId);
                contest.Complete();
                contest.SubmitForApproval();
                contest.Approve();
                contest.TransitionTo(ContestStatus.Published);
                leagueContests.Add(contest);

                ApplyResult(stats[homeId], stats[awayId], homeGoals, awayGoals);
                results.Add(CreatePublishedResult(
                    DemoId(2000 + ((fixtureIndex - 1) * 2) + 1),
                    contestId,
                    DemoId(5000 + fixtureIndex),
                    homeId,
                    homeGoals,
                    homeGoals == awayGoals ? 1 : homeGoals > awayGoals ? 1 : 2));
                results.Add(CreatePublishedResult(
                    DemoId(2000 + ((fixtureIndex - 1) * 2) + 2),
                    contestId,
                    DemoId(5000 + fixtureIndex),
                    awayId,
                    awayGoals,
                    homeGoals == awayGoals ? 1 : awayGoals > homeGoals ? 1 : 2));
            }
        }

        context.Contests.AddRange(leagueContests);
        context.ContestResults.AddRange(results);

        var rankedTeamIds = stats
            .OrderByDescending(item => item.Value.Points)
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
                DemoId(3000 + index + 1),
                FootballCompetitionId,
                leagueGroup.Id,
                teamId);
            standing.UpdateStats(
                teamStats.Played,
                teamStats.Won,
                teamStats.Drawn,
                teamStats.Lost,
                teamStats.PointsFor,
                teamStats.PointsAgainst,
                teamStats.Points);
            standing.SetPosition(index + 1);
            context.Standings.Add(standing);
        }

        AddKnockoutDemo(
            context,
            football.Id,
            rulesetVersionId,
            knockoutStage.Id,
            rankedTeamIds);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Contest CreateFootballContest(
        Guid id,
        Guid sportId,
        Guid? rulesetVersionId,
        Guid stageId,
        Guid? groupId,
        int sortOrder,
        string name,
        string venue,
        DateTime startTime,
        Guid? participant1Id = null,
        Guid? participant2Id = null)
    {
        var contest = new Contest(id, NationalSportsTenantId, sportId, "TeamVsTeam");
        contest.SetName(name);
        if (rulesetVersionId.HasValue) contest.AssignRulesetVersion(rulesetVersionId.Value);
        contest.AssignToStage(stageId, sortOrder);
        if (groupId.HasValue) contest.AssignToGroup(groupId.Value);
        if (participant1Id.HasValue) contest.AddParticipant(participant1Id.Value, "Team", 1);
        if (participant2Id.HasValue) contest.AddParticipant(participant2Id.Value, "Team", 2);
        contest.AddSegment("First Half", "Half", 1);
        contest.AddSegment("Second Half", "Half", 2);
        contest.AddSegment("Extra Time", "ExtraTime", 3, true, "ScoresLevelAfterRegulation");
        contest.Schedule(venue, startTime, 120);
        return contest;
    }

    private static ContestResult CreatePublishedResult(
        Guid id,
        Guid contestId,
        Guid eventId,
        Guid participantId,
        int goals,
        int rank)
    {
        var result = new ContestResult(id, contestId, eventId, participantId);
        result.SetScore(goals, goals.ToString(), null);
        result.SetRank(rank, rank == 1 ? "1st" : "2nd");
        result.Submit();
        result.BeginReview();
        result.Approve(SystemAdministratorId);
        result.Publish();
        return result;
    }

    private static void ApplyResult(TeamStats home, TeamStats away, int homeGoals, int awayGoals)
    {
        home.Played++;
        away.Played++;
        home.PointsFor += homeGoals;
        home.PointsAgainst += awayGoals;
        away.PointsFor += awayGoals;
        away.PointsAgainst += homeGoals;

        if (homeGoals > awayGoals)
        {
            home.Won++;
            away.Lost++;
            home.Points += 3;
        }
        else if (awayGoals > homeGoals)
        {
            away.Won++;
            home.Lost++;
            away.Points += 3;
        }
        else
        {
            home.Drawn++;
            away.Drawn++;
            home.Points++;
            away.Points++;
        }
    }

    private static void AddKnockoutDemo(
        SportsPlatformDbContext context,
        Guid sportId,
        Guid? rulesetVersionId,
        Guid knockoutStageId,
        IReadOnlyList<Guid> rankedTeamIds)
    {
        var semiFinal1Id = DemoId(1101);
        var semiFinal2Id = DemoId(1102);
        var finalContestId = DemoId(1103);
        var semiFinal1 = CreateFootballContest(
            semiFinal1Id, sportId, rulesetVersionId, knockoutStageId, null, 1,
            "Semi-final 1: 1st vs 4th", "National Stadium",
            new DateTime(2030, 6, 8, 13, 0, 0, DateTimeKind.Utc),
            rankedTeamIds[0], rankedTeamIds[3]);
        var semiFinal2 = CreateFootballContest(
            semiFinal2Id, sportId, rulesetVersionId, knockoutStageId, null, 2,
            "Semi-final 2: 2nd vs 3rd", "National Stadium",
            new DateTime(2030, 6, 8, 16, 0, 0, DateTimeKind.Utc),
            rankedTeamIds[1], rankedTeamIds[2]);
        var finalContest = CreateFootballContest(
            finalContestId, sportId, rulesetVersionId, knockoutStageId, null, 3,
            "Championship Final", "National Stadium",
            new DateTime(2030, 6, 15, 15, 0, 0, DateTimeKind.Utc));
        context.Contests.AddRange(semiFinal1, semiFinal2, finalContest);

        var bracket = new Bracket(
            FootballBracketId,
            FootballCompetitionId,
            "Top Four Championship Bracket",
            BracketType.SingleElimination,
            4);
        bracket.SetSeeded(true);
        bracket.SetDrawSeed(2030);

        var semiFinalRound = new BracketRound(DemoId(4010), bracket.Id, 1, "Semi-finals", 2);
        semiFinalRound.SetScheduledAt(new DateTime(2030, 6, 8, 13, 0, 0, DateTimeKind.Utc));
        var finalRound = new BracketRound(DemoId(4020), bracket.Id, 2, "Final", 1);
        finalRound.SetScheduledAt(new DateTime(2030, 6, 15, 15, 0, 0, DateTimeKind.Utc));

        var semiFinalMatch1 = new BracketMatch(DemoId(4031), semiFinalRound.Id, 1);
        semiFinalMatch1.SetParticipants(rankedTeamIds[0], rankedTeamIds[3]);
        semiFinalMatch1.BindContest(semiFinal1Id);
        var semiFinalMatch2 = new BracketMatch(DemoId(4032), semiFinalRound.Id, 2);
        semiFinalMatch2.SetParticipants(rankedTeamIds[1], rankedTeamIds[2]);
        semiFinalMatch2.BindContest(semiFinal2Id);
        var finalMatch = new BracketMatch(DemoId(4033), finalRound.Id, 1);
        finalMatch.BindContest(finalContestId);
        semiFinalMatch1.AdvanceTo(finalMatch.Id, 1);
        semiFinalMatch2.AdvanceTo(finalMatch.Id, 2);

        semiFinalRound.AddMatch(semiFinalMatch1);
        semiFinalRound.AddMatch(semiFinalMatch2);
        finalRound.AddMatch(finalMatch);
        bracket.AddRound(semiFinalRound);
        bracket.AddRound(finalRound);
        bracket.Publish();
        bracket.Start();
        context.Brackets.Add(bracket);
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"10000000-0000-0000-0000-{value:D12}");

    private sealed record DemoTenantDefinition(
        Guid TenantId,
        Guid BrandingId,
        string Name,
        string ShortName,
        string Slug,
        string Description,
        string PrimaryColor,
        string SecondaryColor,
        string AccentColor,
        string Email);

    private sealed class TeamStats
    {
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int PointsFor { get; set; }
        public int PointsAgainst { get; set; }
        public decimal Points { get; set; }
    }
}
