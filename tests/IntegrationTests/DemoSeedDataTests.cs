using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Persistence.SeedData;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Sports.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class DemoSeedDataTests
{
    [Fact]
    public async Task Registration_demo_seed_covers_forms_policies_and_every_workflow_state()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"registration-demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        await SeedData.InitializeAsync(context);
        await RegistrationDemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var forms = await context.RegistrationForms
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToArrayAsync();
        forms.Should().Contain(item => item.Id == RegistrationDemoSeedData.TeamEntryFormId
            && item.CompetitionId == DemoSeedData.FootballCompetitionId
            && item.IsActive
            && item.Version == 2);
        forms.Should().Contain(item => item.Id == RegistrationDemoSeedData.AthleteProfileFormId
            && item.CompetitionId == null
            && item.IsActive);
        forms.Should().Contain(item => item.Id == RegistrationDemoSeedData.ArchivedFormId
            && !item.IsActive);

        var policy = await context.CompetitionEligibilityPolicies.SingleAsync(
            item => item.Id == RegistrationDemoSeedData.FootballPolicyId);
        policy.MinimumSquadSize.Should().Be(15);
        policy.MaximumSquadSize.Should().Be(30);
        policy.RequiredDocumentTypes.Should().Contain("Medical clearance");
        policy.RequireUniqueSquadNumbers.Should().BeTrue();

        var registrations = await context.Registrations
            .Where(item => item.Id.ToString().StartsWith("80000000-0000-0000-0000-0000000003"))
            .ToArrayAsync();
        registrations.Should().HaveCount(10);
        registrations.Select(item => item.Status).Should().BeEquivalentTo(
            Enum.GetValues<RegistrationStatus>());
        registrations.Should().Contain(item => item.EligibilityOverriddenBy ==
            Guid.Parse("00000000-0000-0000-0000-000000000020"));
        registrations.Should().Contain(item => item.EntryFee == 2500m && item.IsEntryFeePaid);
        registrations.Should().Contain(item => item.EntryFee == 2500m && !item.IsEntryFeePaid);

        var submissions = await context.RegistrationFormSubmissions
            .Where(item => item.FormId == RegistrationDemoSeedData.TeamEntryFormId)
            .ToArrayAsync();
        submissions.Should().HaveCount(6);
        submissions.Select(item => item.Status).Should().BeEquivalentTo(
            Enum.GetValues<RegistrationFormSubmissionStatus>());
        submissions.Should().OnlyContain(item => item.FormVersion == 2 && item.Revision == 2);
        submissions.Should().OnlyContain(item => item.RegistrationId.HasValue);
        registrations.Count(item => item.FormSubmissionId.HasValue).Should().Be(6);
    }

    [Fact]
    public async Task Walkthrough_seed_contains_only_bootstrap_data_and_one_user()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"walkthrough-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        await WalkthroughSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        (await context.Tenants.CountAsync()).Should().Be(1);
        (await context.Users.CountAsync()).Should().Be(1);
        (await context.Users.SingleAsync()).Email.Should()
            .Be("admin@sportsplatform.com");
        (await context.Competitions.CountAsync()).Should().Be(0);
        (await context.Participants.CountAsync()).Should().Be(0);
        (await context.Contests.CountAsync()).Should().Be(0);
        (await context.Sports.CountAsync()).Should().Be(12);
        (await context.Sports.Include(item => item.Versions).ToArrayAsync())
            .Should().OnlyContain(item => item.Versions.Any(version => version.IsPublished));
    }

    [Fact]
    public async Task Historical_timeline_backfill_covers_every_seeded_public_result()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"historical-timeline-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        await SeedData.InitializeAsync(context);
        await HistoricalTimelineSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var resultContestIds = await context.ContestResults
            .Where(item => item.Status == ContestResultStatus.Published
                || item.Status == ContestResultStatus.Official)
            .Select(item => item.ContestId)
            .Distinct()
            .ToArrayAsync();
        var timelineContestIds = await context.LiveScores
            .Where(item => resultContestIds.Contains(item.ContestId))
            .Select(item => item.ContestId)
            .Distinct()
            .ToArrayAsync();

        timelineContestIds.Should().BeEquivalentTo(resultContestIds);
        var backfilledContestId = await context.LiveScores
            .Where(item => item.DeviceId == "historical-timeline-backfill")
            .Select(item => item.ContestId)
            .FirstAsync();
        var orderedTypes = await context.LiveScores
            .Where(item => item.ContestId == backfilledContestId)
            .OrderBy(item => item.TimelineSequence)
            .Select(item => item.EventType)
            .ToArrayAsync();
        orderedTypes.First().Should().Be("MATCH_START");
        orderedTypes.Last().Should().Be("MATCH_END");
    }

    [Fact]
    public async Task InitializeAsync_schedules_canonical_team_sport_fixtures_for_public_views()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"canonical-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        await SeedData.InitializeAsync(context);
        context.ChangeTracker.Clear();

        var competitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");
        var stageIds = await context.Competitions
            .Where(item => item.Id == competitionId)
            .SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .Select(item => item.Id)
            .ToArrayAsync();
        var fixtures = await context.Contests
            .Where(item => item.StageId.HasValue && stageIds.Contains(item.StageId.Value))
            .ToArrayAsync();

        fixtures.Should().HaveCount(28);
        fixtures.Should().OnlyContain(item =>
            item.Status == ContestStatus.Scheduled
            && item.StartTime.HasValue
            && item.Venue == "Premier League venue TBC");
    }

    [Fact]
    public async Task InitializeAsync_creates_a_complete_public_e2e_showcase()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"public-e2e-showcase-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        await SeedData.InitializeAsync(context);
        await PublicE2eShowcaseSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var tenant = await context.Tenants.SingleAsync(
            item => item.Id == PublicE2eShowcaseSeedData.TenantId);
        tenant.Slug.Should().Be("public-e2e-showcase");
        tenant.Name.Should().Be("Public E2E Sports Showcase");

        var enabledSports = await context.TenantSports
            .Where(item => item.TenantId == tenant.Id && item.IsEnabled)
            .Select(item => item.SportId)
            .Distinct()
            .ToArrayAsync();
        enabledSports.Should().HaveCount(7);

        var competitions = await context.Competitions
            .Include(item => item.Editions)
            .ThenInclude(item => item.Stages)
            .Where(item => item.TenantId == tenant.Id)
            .ToArrayAsync();
        competitions.Should().HaveCount(9);
        competitions.Should().OnlyContain(item => item.Status == CompetitionStatus.Published);
        competitions.Select(item => item.SportId).Distinct().Should().HaveCount(7);
        competitions.SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .Select(item => item.Type)
            .Should().Contain(new[]
            {
                StageType.Group,
                StageType.Knockout,
                StageType.GroupAndKnockout,
                StageType.League,
                StageType.Leaderboard,
                StageType.Heat,
                StageType.FightCard
            });

        var contests = await context.Contests
            .Where(item => item.TenantId == tenant.Id)
            .ToArrayAsync();
        contests.Should().HaveCount(36);
        contests.Count(item => item.Status == ContestStatus.Published).Should().Be(9);
        contests.Count(item => item.Status == ContestStatus.InProgress).Should().Be(9);
        contests.Count(item => item.Status == ContestStatus.Ready).Should().Be(9);
        contests.Count(item => item.Status == ContestStatus.Scheduled).Should().Be(9);
        contests.Where(item => item.ContestShape == "Bout")
            .OrderBy(item => item.SortOrder)
            .Select(item => item.CardSection)
            .Should().Equal("PreliminaryCard", "MainCard", "CoMainEvent", "MainEvent");
        contests.Where(item => item.Status == ContestStatus.Published)
            .Should().OnlyContain(item => item.StartTime < DateTime.UtcNow);
        contests.Where(item => item.Status is ContestStatus.Ready or ContestStatus.Scheduled)
            .Should().OnlyContain(item => item.StartTime > DateTime.UtcNow);

        var participants = await context.Participants
            .Where(item => item.TenantId == tenant.Id)
            .ToArrayAsync();
        participants.Should().HaveCount(36);
        participants.Select(item => item.Name).Should().OnlyHaveUniqueItems();
        participants.Should().OnlyContain(item =>
            !item.Name.Contains("Demo", StringComparison.OrdinalIgnoreCase)
            && !item.Name.Any(char.IsDigit));

        var results = await context.ContestResults
            .Where(item => contests.Select(contest => contest.Id).Contains(item.ContestId))
            .ToArrayAsync();
        results.Should().HaveCount(22);
        results.Should().OnlyContain(item => item.Status == ContestResultStatus.Published);

        (await context.Standings.CountAsync(item =>
            competitions.Select(competition => competition.Id).Contains(item.CompetitionId)))
            .Should().Be(20);
        (await context.Brackets.CountAsync(item => item.CompetitionId ==
            Guid.Parse("80000000-0000-0000-0000-000000000102")))
            .Should().Be(1);
        (await context.LiveScores.CountAsync(item => item.TenantId == tenant.Id
            && item.MetricName == "strokes")).Should().Be(72);
    }

    [Fact]
    public async Task EnsureAsync_creates_an_idempotent_isolated_complete_football_demo()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var football = new Sport(Guid.NewGuid(), "FOOTBALL", "Football");
        var ruleset = football.CreateNewVersion("Demo football rules", "test");
        ruleset.ConfigureContestShape(
            "TeamVsTeam", "Team", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
        ruleset.Publish();
        context.Sports.Add(football);
        await context.SaveChangesAsync();

        await DemoSeedData.EnsureAsync(context);
        await DemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var demoTenantIds = new[]
        {
            DemoSeedData.NationalSportsTenantId,
            DemoSeedData.SchoolGamesTenantId,
            DemoSeedData.ShortTournamentTenantId
        };
        var demoTenants = await context.Tenants
            .Where(item => demoTenantIds.Contains(item.Id))
            .ToListAsync();
        demoTenants.Should().HaveCount(3);
        demoTenants.Select(item => item.Slug).Should().OnlyHaveUniqueItems();

        var brandings = await context.TenantBrandings
            .Where(item => demoTenantIds.Contains(item.TenantId))
            .ToListAsync();
        brandings.Should().HaveCount(3);
        brandings.Select(item => item.PrimaryColor).Should().OnlyHaveUniqueItems();

        var competition = await context.Competitions
            .SingleAsync(item => item.Id == DemoSeedData.FootballCompetitionId);
        competition.TenantId.Should().Be(DemoSeedData.NationalSportsTenantId);
        competition.RulesetVersionId.Should().Be(ruleset.Id);

        var teams = await context.Participants.OfType<Team>()
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToListAsync();
        teams.Should().HaveCount(8);
        (await context.Participants.CountAsync(item =>
            item.TenantId == DemoSeedData.SchoolGamesTenantId
            || item.TenantId == DemoSeedData.ShortTournamentTenantId)).Should().Be(0);

        var contests = await context.Contests
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToListAsync();
        contests.Should().HaveCount(31);
        contests.Count(item => item.Status == ContestStatus.Published).Should().Be(28);
        contests.Count(item => item.Status == ContestStatus.Scheduled).Should().Be(3);
        contests.Should().OnlyContain(item => item.RulesetVersionId == ruleset.Id);

        var results = await context.ContestResults.ToListAsync();
        results.Should().HaveCount(56);
        results.Should().OnlyContain(item => item.Status == ContestResultStatus.Published);

        var standings = await context.Standings
            .Where(item => item.CompetitionId == DemoSeedData.FootballCompetitionId)
            .OrderBy(item => item.Position)
            .ToListAsync();
        standings.Should().HaveCount(8);
        standings.Should().OnlyContain(item => item.Played == 7);
        standings.Select(item => item.Position).Should().Equal(Enumerable.Range(1, 8));

        var bracket = await context.Brackets
            .Include(item => item.Rounds)
            .ThenInclude(item => item.Matches)
            .SingleAsync(item => item.Id == DemoSeedData.FootballBracketId);
        bracket.Status.Should().Be(BracketStatus.InProgress);
        bracket.Rounds.Should().HaveCount(2);
        bracket.Rounds.SelectMany(item => item.Matches).Should().HaveCount(3);
        var semiFinalists = bracket.Rounds.Single(item => item.RoundNumber == 1)
            .Matches.SelectMany(item => new[] { item.Participant1Id, item.Participant2Id });
        semiFinalists.Should().BeEquivalentTo(
            standings.Take(4).Select(item => (Guid?)item.ParticipantId));
    }

    [Fact]
    public async Task EnsureAsync_creates_an_idempotent_resumable_basketball_series_demo()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"basketball-demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var basketball = new Sport(Guid.NewGuid(), "BASKETBALL", "Basketball");
        var ruleset = basketball.CreateNewVersion("Demo basketball rules", "test");
        ruleset.ConfigureContestShape(
            "TeamVsTeam", "Team", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
        ruleset.Publish();
        context.Sports.Add(basketball);
        await context.SaveChangesAsync();

        await DemoSeedData.EnsureAsync(context);
        await DemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var competition = await context.Competitions.SingleAsync(
            item => item.Id == BasketballDemoSeedData.CompetitionId);
        competition.TenantId.Should().Be(DemoSeedData.SchoolGamesTenantId);
        competition.RulesetVersionId.Should().Be(ruleset.Id);

        var teams = await context.Participants.OfType<Team>()
            .Where(item => item.TenantId == DemoSeedData.SchoolGamesTenantId)
            .ToListAsync();
        teams.Should().HaveCount(6);
        teams.Select(item => item.Name).Should().OnlyHaveUniqueItems();
        (await context.Participants.CountAsync(item =>
            item.TenantId == DemoSeedData.NationalSportsTenantId
            || item.TenantId == DemoSeedData.ShortTournamentTenantId)).Should().Be(0);

        var contests = await context.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .Where(item => item.TenantId == DemoSeedData.SchoolGamesTenantId)
            .ToListAsync();
        contests.Should().HaveCount(24);
        contests.Count(item => item.Status == ContestStatus.Approved).Should().Be(17);
        contests.Count(item => item.Status == ContestStatus.Scheduled).Should().Be(7);
        contests.Should().OnlyContain(item => item.RulesetVersionId == ruleset.Id);
        contests.Should().OnlyContain(item => item.Segments.Count == 5);

        var seriesGames = contests
            .Where(item => item.SeriesId.HasValue)
            .OrderBy(item => item.SeriesId)
            .ThenBy(item => item.SeriesGameNumber)
            .ToList();
        seriesGames.Should().HaveCount(9);
        seriesGames.Select(item => item.SeriesId).Distinct().Should().HaveCount(3);
        seriesGames.GroupBy(item => item.SeriesId).Should().OnlyContain(group =>
            group.Select(item => item.SeriesGameNumber).SequenceEqual(new int?[] { 1, 2, 3 })
            && group.Select(item => item.SeriesHomeParticipantSlot)
                .SequenceEqual(new int?[] { 1, 2, 1 }));
        seriesGames.Count(item => item.Status == ContestStatus.Approved).Should().Be(2);
        seriesGames.Count(item => item.Status == ContestStatus.Scheduled).Should().Be(7);
        seriesGames.Where(item => item.Name!.StartsWith("Championship Final"))
            .Should().OnlyContain(item => item.Participants.Count == 0);

        var results = await context.ContestResults.ToListAsync();
        results.Should().HaveCount(34);
        results.Should().OnlyContain(item => item.Status == ContestResultStatus.Published);

        var standings = await context.Standings
            .Where(item => item.CompetitionId == BasketballDemoSeedData.CompetitionId)
            .OrderBy(item => item.Position)
            .ToListAsync();
        standings.Should().HaveCount(6);
        standings.Should().OnlyContain(item => item.Played == 5);
        standings.Select(item => item.Position).Should().Equal(Enumerable.Range(1, 6));

        var bracket = await context.Brackets
            .Include(item => item.Rounds)
            .ThenInclude(item => item.Matches)
            .SingleAsync(item => item.Id == BasketballDemoSeedData.BracketId);
        bracket.Status.Should().Be(BracketStatus.InProgress);
        bracket.Rounds.Should().HaveCount(2);
        var matches = bracket.Rounds.SelectMany(item => item.Matches).ToList();
        matches.Should().HaveCount(3);
        matches.Should().OnlyContain(item => item.BestOf == 3 && item.SeriesId.HasValue);
        var semifinals = bracket.Rounds.Single(item => item.RoundNumber == 1).Matches;
        semifinals.Should().OnlyContain(item =>
            item.Participant1Id.HasValue
            && item.Participant2Id.HasValue
            && item.Score1 == "1"
            && item.Score2 == "0");
        var final = bracket.Rounds.Single(item => item.RoundNumber == 2).Matches.Single();
        final.Participant1Id.Should().BeNull();
        final.Participant2Id.Should().BeNull();
    }

    [Fact]
    public async Task EnsureAsync_creates_an_idempotent_golf_cut_with_an_open_final_round()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"golf-demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var golf = new Sport(
            Guid.Parse("00000000-0000-0000-0000-000000000102"),
            "GOLF",
            "Golf");
        var ruleset = golf.CreateNewVersion("Demo golf rules", "test");
        ruleset.ConfigureContestShape(
            "Individual", "Athlete", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
        ruleset.Publish();
        context.Sports.Add(golf);
        await context.SaveChangesAsync();

        await DemoSeedData.EnsureAsync(context);
        await DemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var competition = await context.Competitions.SingleAsync(
            item => item.Id == GolfDemoSeedData.CompetitionId);
        competition.TenantId.Should().Be(DemoSeedData.ShortTournamentTenantId);
        competition.RulesetVersionId.Should().Be(ruleset.Id);

        var golfers = await context.Participants.OfType<Athlete>()
            .Where(item => item.TenantId == DemoSeedData.ShortTournamentTenantId)
            .OrderBy(item => item.Name)
            .ToListAsync();
        golfers.Should().HaveCount(24);
        (await context.Participants.CountAsync(item =>
            item.TenantId == DemoSeedData.NationalSportsTenantId
            || item.TenantId == DemoSeedData.SchoolGamesTenantId)).Should().Be(0);

        var contest = await context.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .SingleAsync(item => item.Id == GolfDemoSeedData.ContestId);
        contest.TenantId.Should().Be(DemoSeedData.ShortTournamentTenantId);
        contest.Status.Should().Be(ContestStatus.InProgress);
        contest.ContestShape.Should().Be("MultiRoundLeaderboard");
        contest.RulesetVersionId.Should().Be(ruleset.Id);
        contest.ParPerRound.Should().Be(72);
        contest.CutAfterRound.Should().Be(2);
        contest.CutToParticipants.Should().Be(12);
        contest.CutAppliedAt.Should().NotBeNull();
        contest.CourseName.Should().Be("Savannah Hills Golf Club");
        contest.Participants.Should().HaveCount(24);
        contest.Participants.Count(item => item.MadeCut == true).Should().Be(13);
        contest.Participants.Count(item => item.MadeCut == false).Should().Be(11);
        contest.Segments.Should().HaveCount(4);
        contest.Segments.Should().OnlyContain(item => item.NestedSegments.Count == 18);
        contest.Segments.OrderBy(item => item.SortOrder).Take(3)
            .Should().OnlyContain(item => item.Status == SegmentStatus.Completed);
        contest.Segments.Single(item => item.SortOrder == 4).Status
            .Should().Be(SegmentStatus.InProgress);

        var scores = await context.LiveScores
            .Where(item => item.ContestId == GolfDemoSeedData.ContestId)
            .OrderBy(item => item.Sequence)
            .ToListAsync();
        scores.Should().HaveCount(1098);
        scores.Should().OnlyContain(item =>
            item.TenantId == DemoSeedData.ShortTournamentTenantId
            && item.MetricName == "strokes"
            && item.RulesetVersionId == ruleset.Id);
        scores.Select(item => item.Sequence).Should().Equal(Enumerable.Range(1, 1098));
        scores.Select(item => item.IdempotencyKey).Should().OnlyHaveUniqueItems();

        var scoreCounts = scores.GroupBy(item => item.ParticipantId)
            .ToDictionary(item => item.Key, item => item.Count());
        foreach (var participant in contest.Participants)
        {
            scoreCounts[participant.ParticipantId].Should()
                .Be(participant.MadeCut == true ? 54 : 36);
        }

        var roundByHole = contest.Segments.SelectMany(round =>
                round.NestedSegments.Select(hole => new { hole.Id, round.SortOrder }))
            .ToDictionary(item => item.Id, item => item.SortOrder);
        var cutTotals = scores
            .Where(item => roundByHole[item.SegmentId!.Value] <= 2)
            .GroupBy(item => item.ParticipantId)
            .ToDictionary(item => item.Key, item => item.Sum(score => score.Value));
        var golfer12 = golfers.Single(item => item.Name.EndsWith("12"));
        var golfer13 = golfers.Single(item => item.Name.EndsWith("13"));
        cutTotals[golfer12.Id].Should().Be(cutTotals[golfer13.Id]);
        contest.Participants.Single(item => item.ParticipantId == golfer12.Id).MadeCut
            .Should().BeTrue();
        contest.Participants.Single(item => item.ParticipantId == golfer13.Id).MadeCut
            .Should().BeTrue();
        cutTotals.Values.Count(total => total <= cutTotals[golfer12.Id]).Should().Be(13);
    }

    [Fact]
    public async Task EnsureAsync_creates_an_idempotent_complete_eight_bout_combat_card()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"combat-demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var combat = new Sport(
            Guid.Parse("00000000-0000-0000-0000-000000000105"),
            "MMA",
            "Combat Sports");
        var ruleset = combat.CreateNewVersion("Demo combat rules", "test");
        ruleset.ConfigureContestShape(
            "FighterVsFighter", "Athlete", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
        ruleset.Publish();
        context.Sports.Add(combat);
        await context.SaveChangesAsync();

        await DemoSeedData.EnsureAsync(context);
        await DemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var competition = await context.Competitions.SingleAsync(
            item => item.Id == CombatDemoSeedData.CompetitionId);
        competition.TenantId.Should().Be(DemoSeedData.NationalSportsTenantId);
        competition.RulesetVersionId.Should().Be(ruleset.Id);

        var fighters = await context.Participants.OfType<Athlete>()
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToListAsync();
        fighters.Should().HaveCount(16);
        (await context.Participants.CountAsync(item =>
            item.TenantId == DemoSeedData.SchoolGamesTenantId
            || item.TenantId == DemoSeedData.ShortTournamentTenantId)).Should().Be(0);

        var bouts = await context.Contests
            .Include(item => item.Participants)
            .Include(item => item.Officials)
            .Include(item => item.Segments)
            .Include(item => item.Events)
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        bouts.Should().HaveCount(8);
        bouts.Select(item => item.SortOrder).Should().Equal(Enumerable.Range(1, 8));
        bouts.Should().OnlyContain(item =>
            item.ContestShape == "Bout"
            && item.Status == ContestStatus.Completed
            && item.RulesetVersionId == ruleset.Id
            && item.WinnerId.HasValue
            && item.Participants.Count == 2
            && item.Officials.Count == 4);
        bouts.SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId).Should().OnlyHaveUniqueItems();
        bouts.Should().OnlyContain(item =>
            item.Officials.Count(official => official.Role == "Referee") == 1
            && item.Officials.Count(official => official.Role == "Judge") == 3
            && item.Officials.All(official => !string.IsNullOrWhiteSpace(official.DisplayName)));
        bouts.Select(item => item.CardSection).Should().Equal(
            "PreliminaryCard", "PreliminaryCard", "PreliminaryCard", "PreliminaryCard",
            "MainCard", "MainCard", "CoMainEvent", "MainEvent");
        bouts.Take(7).Should().OnlyContain(item =>
            item.ScheduledRounds == 3 && item.Segments.Count == 3);
        bouts[7].ScheduledRounds.Should().Be(5);
        bouts[7].Segments.Should().HaveCount(5);
        bouts.Should().OnlyContain(item => item.GetAllowedResultMethods().SequenceEqual(
            new[] { "Unanimous Decision", "Knockout", "Submission" }));
        bouts.Select(item => item.ResultMethod).Should().Contain(
            new[] { "Unanimous Decision", "Knockout", "Submission" });

        var decisionBouts = bouts.Where(item => item.ResultMethod == "Unanimous Decision")
            .ToList();
        decisionBouts.Should().HaveCount(3);
        decisionBouts.Should().OnlyContain(item =>
            item.ResultRound == item.ScheduledRounds
            && item.ResultTimeSeconds == null
            && item.Events.Count == 3
            && item.Events.All(scorecard =>
                scorecard.EventType == "JudgeScorecard"
                && scorecard.Status == EventStatus.Approved));
        foreach (var bout in decisionBouts)
        {
            foreach (var scorecard in bout.Events)
            {
                using var document = JsonDocument.Parse(scorecard.MetricValue!);
                document.RootElement.GetProperty("OfficialId").GetGuid().Should()
                    .Be(scorecard.ScorerId.GetValueOrDefault());
                document.RootElement.GetProperty("Rounds").GetArrayLength().Should()
                    .Be(bout.ScheduledRounds);
            }
        }

        bouts.Where(item => item.ResultMethod != "Unanimous Decision")
            .Should().OnlyContain(item =>
                item.ResultRound == 2
                && item.ResultTimeSeconds == 145
                && item.Events.Count == 0);
    }

    [Fact]
    public async Task EnsureAsync_creates_an_idempotent_complete_athletics_qualification_demo()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"athletics-demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var athletics = new Sport(
            Guid.Parse("00000000-0000-0000-0000-000000000103"),
            "ATHLETICS",
            "Athletics");
        var ruleset = athletics.CreateNewVersion("Demo athletics rules", "test");
        ruleset.ConfigureContestShape(
            "Individual", "Athlete", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
        ruleset.Publish();
        context.Sports.Add(athletics);
        await context.SaveChangesAsync();

        await DemoSeedData.EnsureAsync(context);
        await DemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var competition = await context.Competitions.SingleAsync(
            item => item.Id == AthleticsDemoSeedData.CompetitionId);
        competition.TenantId.Should().Be(DemoSeedData.NationalSportsTenantId);
        competition.RulesetVersionId.Should().Be(ruleset.Id);

        var athletes = await context.Participants.OfType<Athlete>()
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToListAsync();
        athletes.Should().HaveCount(16);
        (await context.Participants.CountAsync(item =>
            item.TenantId == DemoSeedData.SchoolGamesTenantId
            || item.TenantId == DemoSeedData.ShortTournamentTenantId)).Should().Be(0);

        var races = await context.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        races.Should().HaveCount(5);
        races.Select(item => item.SortOrder).Should().Equal(Enumerable.Range(1, 5));
        races.Should().OnlyContain(item =>
            item.Status == ContestStatus.Completed
            && item.RulesetVersionId == ruleset.Id
            && item.AthleticsEventName == "Men's 100m"
            && item.DistanceMeters == 100
            && item.AthleticsLanesPerHeat == 8
            && item.AthleticsLaneAssignment == "Seeded"
            && item.Segments.Count == 1
            && item.Segments.Single().Status == SegmentStatus.Completed);

        var heats = races.Where(item => item.ContestShape == "Heat").ToArray();
        var semifinals = races.Where(item => item.ContestShape == "AthleticsSemifinal")
            .ToArray();
        var final = races.Single(item => item.ContestShape == "AthleticsFinal");
        heats.Should().HaveCount(2);
        semifinals.Should().HaveCount(2);
        heats.Should().OnlyContain(item =>
            item.Participants.Count == 8
            && item.QualifyByPlace == 3
            && item.QualifyByTime == 2
            && item.NextRoundHeatCount == 2
            && item.NextRoundQualifyByPlace == 2
            && item.NextRoundQualifyByTime == 2
            && item.QualificationAppliedAt.HasValue);
        heats.SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId).Should().OnlyHaveUniqueItems();
        heats.Should().OnlyContain(item =>
            item.Participants.OrderBy(participant => participant.SortOrder)
                .Select(participant => participant.SortOrder)
                .SequenceEqual(Enumerable.Range(1, 8)));

        var heatEntries = heats.SelectMany(item => item.Participants).ToArray();
        heatEntries.Count(item => item.QualificationStatus == "Qualified").Should().Be(8);
        heatEntries.Count(item => item.QualificationStatus == "Eliminated").Should().Be(7);
        heatEntries.Count(item => item.Status == ContestParticipantStatus.Disqualified)
            .Should().Be(1);
        heatEntries.Single(item => item.Status == ContestParticipantStatus.Disqualified)
            .QualificationReason.Should().Be("False start");
        heatEntries.Count(item => item.QualificationReason!.Contains("by place"))
            .Should().Be(6);
        heatEntries.Count(item => item.QualificationReason!.Contains("by time"))
            .Should().Be(2);

        semifinals.Should().OnlyContain(item =>
            item.Participants.Count == 4
            && item.QualifyByPlace == 2
            && item.QualifyByTime == 2
            && item.QualificationAppliedAt.HasValue);
        semifinals.Should().OnlyContain(item =>
            item.Participants.Select(participant => participant.SortOrder)
                .OrderBy(lane => lane)
                .SequenceEqual(new[] { 3, 4, 5, 6 }));
        var semifinalEntries = semifinals.SelectMany(item => item.Participants).ToArray();
        semifinalEntries.Select(item => item.ParticipantId).Should().OnlyHaveUniqueItems();
        semifinalEntries.Count(item => item.QualificationStatus == "Qualified").Should().Be(6);
        semifinalEntries.Count(item => item.QualificationStatus == "Eliminated").Should().Be(2);
        semifinalEntries.Count(item => item.QualificationReason!.Contains("by place"))
            .Should().Be(4);
        semifinalEntries.Count(item => item.QualificationReason!.Contains("by time"))
            .Should().Be(2);

        final.Participants.Should().HaveCount(6);
        final.Participants.Select(item => item.SortOrder).OrderBy(lane => lane)
            .Should().Equal(Enumerable.Range(2, 6));
        final.Participants.Select(item => item.ParticipantId).Should().BeEquivalentTo(
            semifinalEntries.Where(item => item.QualificationStatus == "Qualified")
                .Select(item => item.ParticipantId));
        final.WinnerId.Should().Be(final.Participants.Single(item => item.SortOrder == 4)
            .ParticipantId);

        var scores = await context.LiveScores
            .Where(item => races.Select(race => race.Id).Contains(item.ContestId))
            .ToListAsync();
        scores.Should().HaveCount(29);
        scores.Should().OnlyContain(item =>
            item.TenantId == DemoSeedData.NationalSportsTenantId
            && item.MetricName == "finish_time_ms"
            && item.RulesetVersionId == ruleset.Id);
        scores.Select(item => item.IdempotencyKey).Should().OnlyHaveUniqueItems();
        scores.Select(item => item.Id).Should().OnlyHaveUniqueItems();

        var finalScores = scores.Where(item => item.ContestId == final.Id)
            .OrderBy(item => item.Value)
            .ThenBy(item => final.Participants.Single(participant =>
                participant.ParticipantId == item.ParticipantId).SortOrder)
            .ToArray();
        finalScores.Select(item => item.Value).Should().Equal(
            9_800, 9_900, 10_000, 10_100, 10_100, 10_300);
        var places = finalScores.Select((score, index) =>
            1 + finalScores.Take(index).Count(previous => previous.Value < score.Value))
            .ToArray();
        places.Should().Equal(1, 2, 3, 4, 4, 6);
    }

    [Fact]
    public async Task EnsureAsync_creates_an_idempotent_complete_t20_group_and_knockout_demo()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseInMemoryDatabase($"cricket-demo-seed-{Guid.NewGuid()}")
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var cricket = new Sport(
            Guid.Parse("00000000-0000-0000-0000-000000000104"),
            "CRICKET",
            "Cricket");
        var ruleset = cricket.CreateNewVersion("Demo T20 rules", "test");
        ruleset.ConfigureContestShape(
            "CricketMatch", "Team", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
        ruleset.Publish();
        context.Sports.Add(cricket);
        await context.SaveChangesAsync();

        await DemoSeedData.EnsureAsync(context);
        await DemoSeedData.EnsureAsync(context);
        context.ChangeTracker.Clear();

        var competition = await context.Competitions
            .Include(item => item.Editions)
            .ThenInclude(item => item.Stages)
            .SingleAsync(item => item.Id == CricketDemoSeedData.CompetitionId);
        competition.TenantId.Should().Be(DemoSeedData.NationalSportsTenantId);
        competition.RulesetVersionId.Should().Be(ruleset.Id);
        competition.Editions.Should().ContainSingle();
        competition.Editions.Single().Stages.Select(item => item.Type)
            .Should().BeEquivalentTo(new[] { StageType.Group, StageType.Knockout });

        var teams = await context.Participants.OfType<Team>()
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToListAsync();
        teams.Should().HaveCount(4);
        teams.Select(item => item.Name).Should().BeEquivalentTo(
            "Nairobi Strikers", "Coast Chargers", "Rift Valley Rhinos", "Lake Victoria Kings");
        (await context.Participants.CountAsync(item =>
            item.TenantId == DemoSeedData.SchoolGamesTenantId
            || item.TenantId == DemoSeedData.ShortTournamentTenantId)).Should().Be(0);

        var matches = await context.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .Include(item => item.Events)
            .Where(item => item.TenantId == DemoSeedData.NationalSportsTenantId)
            .ToListAsync();
        matches.Should().HaveCount(9);
        matches.Should().OnlyContain(item =>
            item.Status == ContestStatus.Completed
            && item.RulesetVersionId == ruleset.Id
            && item.WinnerId.HasValue
            && item.Participants.Count == 2
            && item.Segments.Count == 2
            && item.Segments.All(segment =>
                segment.SegmentType == "Innings"
                && segment.Status == SegmentStatus.Completed
                && segment.NestedSegments.Count == 20)
            && item.Events.Count == 42);

        var allEvents = matches.SelectMany(item => item.Events).ToArray();
        allEvents.Should().HaveCount(378);
        allEvents.Should().OnlyContain(item => item.Status == EventStatus.Approved);
        allEvents.Select(item => item.IdempotencyKey).Should().OnlyHaveUniqueItems();
        matches.Should().OnlyContain(item =>
            item.Events.OrderBy(contestEvent => contestEvent.SequenceNumber)
                .Select(contestEvent => contestEvent.SequenceNumber)
                .SequenceEqual(Enumerable.Range(1, 42).Select(value => (long)value)));
        matches.Should().OnlyContain(item =>
            item.Events.Count(contestEvent => contestEvent.EventType == "CricketDelivery") == 40
            && item.Events.Count(contestEvent =>
                contestEvent.EventType == "CricketInningsSummary") == 2);

        foreach (var match in matches)
        {
            var summaries = match.Events
                .Where(item => item.EventType == "CricketInningsSummary")
                .OrderBy(item => item.SequenceNumber)
                .Select(item => JsonDocument.Parse(item.MetricValue!))
                .ToArray();
            try
            {
                summaries.Select(item => item.RootElement.GetProperty("InningsNumber").GetInt32())
                    .Should().Equal(1, 2);
                summaries.Select(item => item.RootElement.GetProperty("Runs").GetInt32())
                    .Should().Equal(120, 100);
                summaries.Should().OnlyContain(item =>
                    item.RootElement.GetProperty("Wickets").GetInt32() == 10
                    && item.RootElement.GetProperty("Balls").GetInt32() == 20
                    && item.RootElement.GetProperty("Extras").GetInt32() == 0);
            }
            finally
            {
                foreach (var summary in summaries) summary.Dispose();
            }

            var deliveries = match.Events
                .Where(item => item.EventType == "CricketDelivery")
                .OrderBy(item => item.SequenceNumber)
                .Select(item => JsonDocument.Parse(item.MetricValue!))
                .ToArray();
            try
            {
                deliveries.Take(20).Select(item =>
                        item.RootElement.GetProperty("InningsNumber").GetInt32())
                    .Should().OnlyContain(item => item == 1);
                deliveries.Skip(20).Select(item =>
                        item.RootElement.GetProperty("InningsNumber").GetInt32())
                    .Should().OnlyContain(item => item == 2);
                deliveries.GroupBy(item =>
                        item.RootElement.GetProperty("InningsNumber").GetInt32())
                    .Should().OnlyContain(group =>
                        group.Count() == 20
                        && group.Count(item =>
                            item.RootElement.GetProperty("IsWicket").GetBoolean()) == 10);
            }
            finally
            {
                foreach (var delivery in deliveries) delivery.Dispose();
            }
        }

        var groupMatches = matches.Where(item => item.ContestShape == "CricketGroupMatch")
            .ToArray();
        var semifinals = matches.Where(item => item.ContestShape == "CricketSemifinal")
            .OrderBy(item => item.SortOrder)
            .ToArray();
        var final = matches.Single(item => item.ContestShape == "CricketFinal");
        groupMatches.Should().HaveCount(6);
        semifinals.Should().HaveCount(2);
        groupMatches.SelectMany(item => item.Participants)
            .GroupBy(item => item.ParticipantId)
            .Should().OnlyContain(group => group.Count() == 3);

        var teamIdsByName = teams.ToDictionary(item => item.Name, item => item.Id);
        var winCounts = teams.ToDictionary(
            item => item.Id,
            item => groupMatches.Count(match => match.WinnerId == item.Id));
        winCounts[teamIdsByName["Nairobi Strikers"]].Should().Be(3);
        winCounts[teamIdsByName["Coast Chargers"]].Should().Be(2);
        winCounts[teamIdsByName["Rift Valley Rhinos"]].Should().Be(1);
        winCounts[teamIdsByName["Lake Victoria Kings"]].Should().Be(0);

        semifinals[0].Participants.Select(item => item.ParticipantId).Should().BeEquivalentTo(
            new[] { teamIdsByName["Nairobi Strikers"], teamIdsByName["Lake Victoria Kings"] });
        semifinals[1].Participants.Select(item => item.ParticipantId).Should().BeEquivalentTo(
            new[] { teamIdsByName["Coast Chargers"], teamIdsByName["Rift Valley Rhinos"] });
        final.Participants.Select(item => item.ParticipantId).Should().Equal(
            teamIdsByName["Nairobi Strikers"], teamIdsByName["Coast Chargers"]);
        final.WinnerId.Should().Be(teamIdsByName["Nairobi Strikers"]);
    }
}
