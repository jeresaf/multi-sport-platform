using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Branding.Entities;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// A single public tenant containing every supported sport, every stage type,
/// and realistic past/live/ready/future scenarios for manual end-to-end testing.
/// The richer sport-specific seeds remain the source for detailed scorecards,
/// brackets, cuts, qualification, and standings algorithms.
/// </summary>
public static class PublicE2eShowcaseSeedData
{
    public static readonly Guid TenantId = Id(1);
    private static readonly Guid SystemAdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    private sealed record ShowcaseDefinition(
        int Number,
        string SportCode,
        string Name,
        StageType StageType,
        string Shape,
        bool UsesTeams,
        string Venue,
        string[] Participants);

    private static readonly ShowcaseDefinition[] Definitions =
    {
        new(1, "FOOTBALL", "East Africa Community Football League", StageType.League,
            "TeamVsTeam", true, "Jamhuri Sports Ground",
            ["Nairobi City Stars", "Coast United", "Rift Valley Rangers", "Lake Region Athletic"]),
        new(2, "FOOTBALL", "County Champions Knockout Cup", StageType.Knockout,
            "TeamVsTeam", true, "Nyayo National Stadium",
            ["Kakamega Green Eagles", "Machakos Leopards", "Nyeri Mountaineers", "Garissa Falcons"]),
        new(3, "BASKETBALL", "National Schools Basketball Championship", StageType.GroupAndKnockout,
            "TeamVsTeam", true, "Kasarani Indoor Arena",
            ["Nairobi Academy Lions", "Mombasa Scholars", "Kisumu Lakeside School", "Nakuru Heights"]),
        new(4, "GOLF", "Savannah Invitational Golf Open", StageType.Leaderboard,
            "MultiRoundLeaderboard", false, "Karen Country Club",
            ["Amani Kamau", "Brian Otieno", "Chebet Kirui", "Diana Wanjiku"]),
        new(5, "ATHLETICS", "National Track and Field Meet", StageType.Heat,
            "Heat", false, "Kipchoge Keino Stadium",
            ["Faith Njeri", "Grace Achieng", "Mercy Jepkosgei", "Zawadi Hassan"]),
        new(6, "CRICKET", "Kenya T20 Counties Championship", StageType.Group,
            "CricketMatch", true, "Ruaraka Sports Club",
            ["Nairobi Strikers", "Coast Chargers", "Rift Valley Rhinos", "Lake Victoria Kings"]),
        new(7, "MMA", "Africa Fight Night", StageType.FightCard,
            "Bout", false, "Kenyatta International Arena",
            ["Abel Mwangi", "Bashir Said", "Caleb Ouma", "Daniel Kiptoo"]),
        new(8, "RUGBY", "Great Rift Rugby Fifteens League", StageType.League,
            "TeamVsTeam", true, "RFUEA Ground",
            ["Nairobi Harlequins", "Coastal Buccaneers", "Rift Valley Bulls", "Kisumu Highlanders"]),
        new(9, "RUGBY", "Kenya Rugby Sevens Festival", StageType.GroupAndKnockout,
            "TeamVsTeam", true, "Mombasa Sports Club",
            ["Nairobi Sevens", "Coast Sevens", "Western Sevens", "Central Sevens"])
    };

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantAsync(context, cancellationToken);
        var sports = await context.Sports.Include(item => item.Versions)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        if (Definitions.Select(item => item.SportCode).Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(code => !sports.ContainsKey(code))) return;

        foreach (var definition in Definitions)
        {
            if (!sports.TryGetValue(definition.SportCode, out var sport)) continue;
            await EnsureTenantSportAsync(context, sport.Id, definition.Number, cancellationToken);
            await EnsureCompetitionAsync(context, sport, definition, cancellationToken);
        }
        await ReconcileFightCardSectionsAsync(context, cancellationToken);
    }

    private static async Task ReconcileFightCardSectionsAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        var competitionId = Id(107);
        var stageIds = await context.Competitions
            .Where(item => item.Id == competitionId)
            .SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .Where(item => item.Type == StageType.FightCard)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var bouts = await context.Contests
            .Where(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value)
                && item.ContestShape == "Bout")
            .OrderBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var sections = new[] { "PreliminaryCard", "MainCard", "CoMainEvent", "MainEvent" };
        for (var index = 0; index < bouts.Length; index++)
        {
            var section = index < sections.Length
                ? sections[index]
                : index == bouts.Length - 1 ? "MainEvent" : "MainCard";
            bouts[index].ConfigureBout(
                section is "MainEvent" or "CoMainEvent" ? "Welterweight" : "Lightweight",
                section,
                section == "MainEvent" ? 5 : 3,
                ["Unanimous Decision", "Knockout", "Submission"]);
        }
        if (bouts.Length > 0) await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureTenantAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.Tenants.AnyAsync(item => item.Id == TenantId, cancellationToken))
        {
            var tenant = new Tenant(TenantId, "Public E2E Sports Showcase", "E2E Sports", "public-e2e-showcase");
            tenant.UpdateDetails(
                "Public E2E Sports Showcase",
                "E2E Sports",
                "A deterministic public test organisation covering every supported sport and lifecycle scenario.");
            tenant.UpdateLocalization("Kenya", "Africa/Nairobi", "KES", "en");
            tenant.UpdateContact("showcase@sportsplatform.local", "+254 700 000 100");
            context.Tenants.Add(tenant);

            var branding = new TenantBranding(Id(2), TenantId, tenant.Name);
            branding.UpdateBranding(tenant.Name, tenant.ShortName, null, "#123B5D", "#F2B134", "#2A9D8F");
            branding.UpdatePublicInfo(tenant.Slug, tenant.ContactEmail, tenant.ContactPhone, null);
            context.TenantBrandings.Add(branding);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureTenantSportAsync(
        SportsPlatformDbContext context,
        Guid sportId,
        int number,
        CancellationToken cancellationToken)
    {
        if (await context.TenantSports.AnyAsync(
                item => item.TenantId == TenantId && item.SportId == sportId,
                cancellationToken)) return;
        context.TenantSports.Add(new TenantSport(Id(20 + number), TenantId, sportId));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureCompetitionAsync(
        SportsPlatformDbContext context,
        Sport sport,
        ShowcaseDefinition definition,
        CancellationToken cancellationToken)
    {
        var competitionId = Id(100 + definition.Number);
        if (await context.Competitions.AnyAsync(item => item.Id == competitionId, cancellationToken)) return;

        var participantIds = new List<Guid>();
        for (var index = 0; index < definition.Participants.Length; index++)
        {
            var participantId = Id((definition.Number * 100) + 200 + index);
            participantIds.Add(participantId);
            Participant participant = definition.UsesTeams
                ? new Team(participantId, TenantId, definition.Participants[index], Initials(definition.Participants[index]))
                : new Athlete(participantId, TenantId, definition.Participants[index], Initials(definition.Participants[index]));
            if (participant is Athlete athlete)
            {
                athlete.UpdatePersonalInfo(
                    new DateTime(1992 + index, 2 + index, 10 + index, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "Kenyan");
                athlete.RegisterForSport(sport.Id);
            }
            else if (participant is Team team)
            {
                team.UpdateDetails(definition.Venue, null, null);
            }
            context.Participants.Add(participant);
        }

        var competition = new Competition(competitionId, TenantId, definition.Name, sport.Id);
        var publishedRulesets = sport.Versions.Where(item => item.IsPublished);
        if (definition.SportCode == "RUGBY")
        {
            var variant = definition.Number == 9 ? RugbyVariants.Sevens : RugbyVariants.Fifteens;
            publishedRulesets = publishedRulesets.Where(item =>
                RugbyVariants.FromValidationConfiguration(item.ValidationConfiguration) == variant);
        }
        var rulesetId = publishedRulesets
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();
        if (rulesetId.HasValue) competition.AssignRulesetVersion(rulesetId.Value);
        competition.Publish();

        var today = DateTime.UtcNow.Date;
        var edition = competition.AddEdition(
            $"{today.Year} Public Test Edition",
            today.AddMonths(-3),
            today.AddMonths(3));
        edition.Open();
        var stage = edition.AddStage(StageName(definition.StageType), definition.StageType, 1);
        stage.Open();
        var standingsGroup = definition.UsesTeams && definition.StageType != StageType.Knockout
            ? stage.AddGroup("Overall Standings")
            : null;
        context.Competitions.Add(competition);

        var scenarios = new[]
        {
            (Suffix: "Completed Result", Start: today.AddDays(-21).AddHours(15), Status: "published"),
            (Suffix: "Live Now", Start: DateTime.UtcNow.AddMinutes(-20), Status: "live"),
            (Suffix: "Check-in Ready", Start: DateTime.UtcNow.AddHours(2), Status: "ready"),
            (Suffix: "Upcoming Fixture", Start: today.AddDays(7).AddHours(14), Status: "scheduled")
        };

        for (var index = 0; index < scenarios.Length; index++)
        {
            var scenario = scenarios[index];
            var contest = new Contest(
                Id((definition.Number * 100) + 1000 + index), TenantId, sport.Id, definition.Shape);
            contest.SetName($"{definition.Name} — {scenario.Suffix}");
            if (rulesetId.HasValue) contest.AssignRulesetVersion(rulesetId.Value);
            contest.AssignToStage(stage.Id, index + 1);
            if (standingsGroup is not null) contest.AssignToGroup(standingsGroup.Id);
            var selected = definition.Shape.Contains("Multi", StringComparison.OrdinalIgnoreCase)
                || definition.Shape == "Heat"
                ? participantIds
                : participantIds.Take(2).ToList();
            foreach (var participantId in selected)
                contest.AddParticipant(participantId, definition.UsesTeams ? "Team" : "Athlete");
            ConfigureSportScenario(contest, definition, index);
            AddSegments(contest, definition.SportCode);
            contest.Schedule(definition.Venue, scenario.Start, Duration(definition.SportCode));

            switch (scenario.Status)
            {
                case "published":
                    contest.Start();
                    if (selected.Count == 2) contest.SetWinner(selected[0]);
                    contest.Complete();
                    contest.SubmitForApproval();
                    contest.Approve();
                    contest.TransitionTo(ContestStatus.Published);
                    AddPublishedResults(context, contest, selected, definition.Number);
                    if (definition.SportCode == "GOLF")
                        AddGolfScores(context, contest, selected, rulesetId);
                    break;
                case "live":
                    contest.Start();
                    break;
                case "ready":
                    contest.OpenCheckIn();
                    contest.MarkReady();
                    break;
            }
            context.Contests.Add(contest);
        }

        if (definition.UsesTeams && definition.StageType != StageType.Knockout)
            AddStandings(
                context, competition.Id, standingsGroup!.Id, participantIds, definition.Number);
        if (definition.StageType == StageType.Knockout)
            AddKnockoutBracket(context, competition.Id, participantIds, definition.Number);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureSportScenario(
        Contest contest,
        ShowcaseDefinition definition,
        int scenarioIndex)
    {
        if (definition.SportCode == "GOLF")
        {
            contest.ConfigureLeaderboard(
                72, 1, 4, "Karen Country Club", "Nairobi, Kenya",
                GolfRulesetService.StandardHolePars);
        }
        else if (definition.SportCode == "ATHLETICS")
        {
            contest.ConfigureAthleticsRace("Women's 100m", 100, lanesPerHeat: 8,
                laneAssignment: "Seeded");
        }
        else if (definition.SportCode == "MMA")
        {
            var sections = new[] { "PreliminaryCard", "MainCard", "CoMainEvent", "MainEvent" };
            var section = sections[Math.Min(scenarioIndex, sections.Length - 1)];
            contest.ConfigureBout(
                section is "MainEvent" or "CoMainEvent" ? "Welterweight" : "Lightweight",
                section,
                section == "MainEvent" ? 5 : 3,
                ["Unanimous Decision", "Knockout", "Submission"]);
        }
    }

    private static void AddStandings(
        SportsPlatformDbContext context,
        Guid competitionId,
        Guid groupId,
        IReadOnlyList<Guid> participantIds,
        int definitionNumber)
    {
        for (var index = 0; index < participantIds.Count; index++)
        {
            var standing = new Standing(
                Id((definitionNumber * 100) + 7000 + index),
                competitionId,
                groupId,
                participantIds[index]);
            standing.UpdateStats(3, 3 - Math.Min(index, 2), index == 3 ? 1 : 0,
                Math.Min(index, 2), 12 - index * 2, 4 + index, 9 - index * 2);
            standing.SetPosition(index + 1);
            context.Standings.Add(standing);
        }
    }

    private static void AddKnockoutBracket(
        SportsPlatformDbContext context,
        Guid competitionId,
        IReadOnlyList<Guid> participants,
        int definitionNumber)
    {
        var bracket = new Bracket(
            Id((definitionNumber * 100) + 8000), competitionId,
            "County Champions Cup Bracket", BracketType.SingleElimination, 4);
        bracket.SetSeeded(true);
        bracket.SetDrawSeed(2026);
        var semifinal = new BracketRound(
            Id((definitionNumber * 100) + 8010), bracket.Id, 1, "Semi-finals", 2);
        var final = new BracketRound(
            Id((definitionNumber * 100) + 8020), bracket.Id, 2, "Final", 1);
        var first = new BracketMatch(Id((definitionNumber * 100) + 8031), semifinal.Id, 1);
        first.SetParticipants(participants[0], participants[3]);
        first.BindContest(Id((definitionNumber * 100) + 1000));
        var second = new BracketMatch(Id((definitionNumber * 100) + 8032), semifinal.Id, 2);
        second.SetParticipants(participants[1], participants[2]);
        second.BindContest(Id((definitionNumber * 100) + 1001));
        var championship = new BracketMatch(Id((definitionNumber * 100) + 8033), final.Id, 1);
        championship.BindContest(Id((definitionNumber * 100) + 1003));
        first.AdvanceTo(championship.Id, 1);
        second.AdvanceTo(championship.Id, 2);
        semifinal.AddMatch(first);
        semifinal.AddMatch(second);
        final.AddMatch(championship);
        bracket.AddRound(semifinal);
        bracket.AddRound(final);
        bracket.Publish();
        bracket.Start();
        context.Brackets.Add(bracket);
    }

    private static void AddGolfScores(
        SportsPlatformDbContext context,
        Contest contest,
        IReadOnlyList<Guid> participants,
        Guid? rulesetId)
    {
        var holes = contest.Segments.Single().NestedSegments.OrderBy(item => item.SortOrder).ToArray();
        var sequence = 0;
        for (var player = 0; player < participants.Count; player++)
        for (var hole = 0; hole < holes.Length; hole++)
        {
            sequence++;
            var strokes = GolfRulesetService.StandardHolePars[hole]
                + ((player + hole) % 7 == 0 ? 1 : 0);
            var liveScore = new LiveScore(
                Id(9000 + sequence), contest.Id, Id(9100 + sequence), participants[player],
                "strokes", strokes, holes[hole].Id, tenantId: TenantId,
                eventType: "HOLE_SCORE", deviceId: "public-showcase-seed",
                idempotencyKey: $"showcase-golf-p{player + 1}-h{hole + 1}",
                rulesetVersionId: rulesetId);
            liveScore.SetSequence(sequence);
            liveScore.SetDisplayValue(strokes.ToString());
            liveScore.SetScorer("system-seed", null);
            context.LiveScores.Add(liveScore);
        }
    }

    private static void AddPublishedResults(
        SportsPlatformDbContext context,
        Contest contest,
        IReadOnlyList<Guid> participants,
        int definitionNumber)
    {
        for (var index = 0; index < participants.Count; index++)
        {
            var rank = index + 1;
            var score = participants.Count == 2 ? (index == 0 ? 3 : 1) : 70 + index * 2;
            var result = new ContestResult(
                Id((definitionNumber * 100) + 5000 + index),
                contest.Id,
                Id((definitionNumber * 100) + 6000 + index),
                participants[index]);
            result.SetScore(score, score.ToString(), null);
            result.SetRank(rank, rank switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{rank}th" });
            result.Submit();
            result.BeginReview();
            result.Approve(SystemAdministratorId);
            result.Publish();
            context.ContestResults.Add(result);
        }
    }

    private static void AddSegments(Contest contest, string sportCode)
    {
        switch (sportCode)
        {
            case "FOOTBALL":
            case "RUGBY":
                contest.AddSegment("First Half", "Half", 1);
                contest.AddSegment("Second Half", "Half", 2);
                break;
            case "BASKETBALL":
                for (var quarter = 1; quarter <= 4; quarter++)
                    contest.AddSegment($"Quarter {quarter}", "Quarter", quarter);
                break;
            case "CRICKET":
                contest.AddSegment("First Innings", "Innings", 1);
                contest.AddSegment("Second Innings", "Innings", 2);
                break;
            case "MMA":
                for (var roundNumber = 1; roundNumber <= 3; roundNumber++)
                    contest.AddSegment($"Round {roundNumber}", "Round", roundNumber);
                break;
            case "GOLF":
                var round = contest.AddSegment("Opening Round", "Round", 1);
                for (var hole = 1; hole <= 18; hole++)
                {
                    var par = GolfRulesetService.StandardHolePars[hole - 1];
                    round.AddNestedSegment($"Hole {hole} (Par {par})", "Hole", hole);
                }
                break;
            case "ATHLETICS":
                contest.AddSegment("Race", "Race", 1);
                break;
        }
    }

    private static int Duration(string sportCode) => sportCode switch
    {
        "RUGBY" => 100,
        "FOOTBALL" => 120,
        "BASKETBALL" => 120,
        "CRICKET" => 210,
        "MMA" => 30,
        "GOLF" => 300,
        _ => 60
    };

    private static string StageName(StageType type) => type switch
    {
        StageType.League => "League Table",
        StageType.Knockout => "Knockout Bracket",
        StageType.GroupAndKnockout => "Groups and Knockout",
        StageType.Leaderboard => "Leaderboard",
        StageType.Heat => "Heats and Finals",
        StageType.FightCard => "Fight Card",
        _ => "Group Stage"
    };

    private static string Initials(string name) => string.Concat(
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3).Select(part => part[0]))
        .ToUpperInvariant();

    private static Guid Id(int value) =>
        Guid.Parse($"80000000-0000-0000-0000-{value:D12}");
}
