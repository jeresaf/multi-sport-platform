using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using SportsPlatform.Modules.Branding.Entities;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Leaderboards.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Sponsorship.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Imports the public staging CSV pack. CSV identifiers are converted to stable GUIDs,
/// while unsupported presentation-only states are mapped to valid domain states.
/// </summary>
public static class PublicCsvSeedData
{
    private static readonly Guid SystemAdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    private static readonly IReadOnlyDictionary<string, string> SportCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["football"] = "FOOTBALL", ["basketball"] = "BASKETBALL",
            ["rugby15"] = "RUGBY", ["rugby7"] = "RUGBY", ["cricket"] = "CRICKET",
            ["golf"] = "GOLF", ["athletics"] = "ATHLETICS", ["mma"] = "MMA",
            ["boxing"] = "BOXING", ["volleyball"] = "VOLLEYBALL",
            ["netball"] = "NETBALL", ["hockey"] = "HOCKEY", ["tennis"] = "TENNIS"
        };

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        string packPath,
        CancellationToken cancellationToken = default)
    {
        ValidatePack(packPath);
        ValidatePackCoverage(packPath);
        var anchorOffset = ResolveAnchorOffset(packPath);
        var tenantRows = Read(packPath, "01_tenants.csv");
        var primaryTenantId = StableId(tenantRows[0]["tenant_id"]);
        if (await context.Tenants.AnyAsync(item => item.Id == primaryTenantId, cancellationToken))
        {
            var competitionCount = await context.Competitions.CountAsync(
                item => item.TenantId == primaryTenantId, cancellationToken);
            var contestCount = await context.Contests.CountAsync(
                item => item.TenantId == primaryTenantId, cancellationToken);
            if (competitionCount >= 18 && contestCount >= 130)
            {
                await ReconcilePresentationAsync(
                    context, packPath, primaryTenantId, cancellationToken);
                return;
            }
            throw new InvalidOperationException(
                "A partial public CSV seed was detected. Reset the staging database before retrying the import.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var sports = await EnsureSportsAsync(context, packPath, cancellationToken);
        var tenants = await SeedTenantsAsync(context, tenantRows, cancellationToken);
        var participantRows = Read(packPath, "04_participants.csv");
        var competitionRows = Read(packPath, "07_competitions.csv");
        await SeedTenantSportsAsync(context, participantRows, competitionRows, tenants, sports, cancellationToken);
        var participants = await SeedParticipantsAsync(context, packPath, participantRows, tenants, sports, cancellationToken);
        var competitions = await SeedCompetitionsAsync(
            context, packPath, competitionRows, tenants, sports, anchorOffset, cancellationToken);
        var contests = await SeedContestsAsync(
            context, packPath, competitions, participants, sports, anchorOffset, cancellationToken);
        await SeedStandingsAsync(context, packPath, competitions, participants, cancellationToken);
        await SeedLeaderboardsAsync(context, packPath, competitions, contests, participants, cancellationToken);
        await SeedBracketsAsync(context, packPath, competitions, contests, participants, cancellationToken);
        await SeedSponsorsAsync(context, packPath, tenants, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await ValidateImportedDataAsync(context, primaryTenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ReconcilePresentationAsync(
        SportsPlatformDbContext context,
        string packPath,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var competitionRows = Read(packPath, "18_presentation_metadata.csv")
            .ToDictionary(row => StableId(row["competition_id"]));
        var competitions = await context.Competitions
            .Where(item => item.TenantId == tenantId
                && competitionRows.Keys.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        foreach (var competition in competitions)
            competition.UpdatePresentationConfiguration(
                CompetitionPresentationConfiguration(competitionRows[competition.Id]));
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        foreach (var row in Read(packPath, "10_contests.csv"))
        {
            var contestId = StableId(row["contest_id"]);
            await InsertPresentationMetricIfMissingAsync(context, tenantId,
                contestId, "periodLabel", row["period_label"], cancellationToken);
            await InsertPresentationMetricIfMissingAsync(context, tenantId,
                contestId, "clock", row["clock"], cancellationToken);
            await InsertPresentationMetricIfMissingAsync(context, tenantId,
                contestId, "summary", row["summary"], cancellationToken);
            await InsertPresentationMetricIfMissingAsync(context, tenantId,
                contestId, "stageLabel", row["round_label"], cancellationToken);
        }

        context.ChangeTracker.Clear();
        var standingRows = Read(packPath, "14_standing_rows.csv");
        var standingMetadata = standingRows.ToDictionary(
            row => StableId($"{row["standing_id"]}:{row["participant_id"]}"),
            row => JsonSerializer.Serialize(new
            {
                form = row["form"],
                bonusPoints = Int(row["bonus_points"]),
                qualificationCode = row["qualification_code"]
            }));
        var standings = await context.Standings
            .Where(item => standingMetadata.Keys.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        foreach (var standing in standings)
            standing.SetMetadata(standingMetadata[standing.Id]);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Sport>> EnsureSportsAsync(
        SportsPlatformDbContext context, string path, CancellationToken cancellationToken)
    {
        var existing = await context.Sports.Include(item => item.Versions)
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var csvSports = Read(path, "02_sports.csv");
        foreach (var row in csvSports)
        {
            var code = SportCodes[row["sport_id"]];
            if (existing.ContainsKey(code)) continue;
            var sport = new Sport(StableId($"seed:sport:{code}"), code, row["name"]);
            sport.UpdateDetails(row["name"], $"Public staging definition for {row["name"]}.", null,
                [row["renderer_kind"] == "MULTI_PARTICIPANT" || row["renderer_kind"] == "COMBAT" ? "athlete" : "team"]);
            var version = sport.CreateNewVersion("Public staging rules", "csv-seed");
            var individual = row["renderer_kind"] is "MULTI_PARTICIPANT" or "COMBAT" or "RACE_HEAT";
            version.ConfigureContestShape(
                Shape(row["renderer_kind"], row["sport_id"]), individual ? "Athlete" : "Team",
                "[]", "[]", "[]", "[]", "[]",
                JsonSerializer.Serialize(new { rendererKind = row["renderer_kind"] }), "{}");
            version.Publish();
            context.Sports.Add(sport);
            existing[code] = sport;
        }
        await context.SaveChangesAsync(cancellationToken);
        return csvSports.ToDictionary(row => row["sport_id"], row => existing[SportCodes[row["sport_id"]]],
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, Guid>> SeedTenantsAsync(
        SportsPlatformDbContext context, IReadOnlyList<Row> rows, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var id = StableId(row["tenant_id"]);
            var shortName = Initials(row["name"]);
            var tenant = new Tenant(id, row["name"], shortName, row["slug"]);
            tenant.UpdateDetails(row["name"], shortName, "Fictional public staging and end-to-end test tenant.");
            tenant.UpdateLocalization(row["country"], row["timezone"], row["currency"], row["language"]);
            context.Tenants.Add(tenant);
            var branding = new TenantBranding(StableId($"branding:{row["tenant_id"]}"), id, row["name"]);
            branding.UpdateBranding(row["name"], shortName, null, row["primary_color"], row["secondary_color"], row["accent_color"]);
            branding.UpdatePublicInfo(row["slug"], null, null, null);
            context.TenantBrandings.Add(branding);
            result[row["tenant_id"]] = id;
        }
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task SeedTenantSportsAsync(
        SportsPlatformDbContext context, IReadOnlyList<Row> participantRows,
        IReadOnlyList<Row> competitionRows, IReadOnlyDictionary<string, Guid> tenants,
        IReadOnlyDictionary<string, Sport> sports, CancellationToken cancellationToken)
    {
        var pairs = participantRows.Select(row => (TenantId: tenants[row["tenant_id"]], SportId: sports[row["sport_id"]].Id))
            .Concat(competitionRows.Select(row => (TenantId: tenants[row["tenant_id"]], SportId: sports[row["sport_id"]].Id)))
            .Distinct();
        foreach (var pair in pairs)
            context.TenantSports.Add(new TenantSport(
                StableId($"tenant-sport:{pair.TenantId}:{pair.SportId}"), pair.TenantId, pair.SportId));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Participant>> SeedParticipantsAsync(
        SportsPlatformDbContext context, string path, IReadOnlyList<Row> rows,
        IReadOnlyDictionary<string, Guid> tenants, IReadOnlyDictionary<string, Sport> sports,
        CancellationToken cancellationToken)
    {
        var teams = Read(path, "05_teams.csv").ToDictionary(row => row["participant_id"], StringComparer.OrdinalIgnoreCase);
        var athletes = Read(path, "06_athletes.csv").ToDictionary(row => row["participant_id"], StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, Participant>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var id = StableId(row["participant_id"]);
            var tenantId = tenants[row["tenant_id"]];
            Participant participant;
            if (row["participant_type"].Equals("TEAM", StringComparison.OrdinalIgnoreCase))
            {
                var teamRow = teams.GetValueOrDefault(row["participant_id"]);
                var team = new Team(id, tenantId, row["display_name"], NonEmpty(row["short_name"], Initials(row["display_name"])));
                team.UpdateProfile(row["display_name"], NonEmpty(row["short_name"], Initials(row["display_name"])),
                    EmptyToNull(teamRow?["logo_url"]), null, null, null, null, null);
                participant = team;
            }
            else
            {
                var athleteRow = athletes.GetValueOrDefault(row["participant_id"]);
                var athlete = new Athlete(id, tenantId, row["display_name"], NonEmpty(row["short_name"], Initials(row["display_name"])));
                athlete.UpdateIdentity(row["display_name"], NonEmpty(row["short_name"], Initials(row["display_name"])),
                    EmptyToNull(athleteRow?["photo_url"]));
                athlete.UpdatePersonalInfo(null, null, EmptyToNull(athleteRow?["country"]));
                athlete.RegisterForSport(sports[row["sport_id"]].Id);
                participant = athlete;
            }
            participant.SetSportAttributes(sports[row["sport_id"]].Id,
                JsonSerializer.Serialize(new { seedKey = row["seed_key"], sportFamily = row["sport_id"] }));
            context.Participants.Add(participant);
            result[row["participant_id"]] = participant;
        }
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private sealed record CompetitionMap(Competition Competition, Dictionary<string, Stage> Stages,
        Dictionary<string, Group> Groups, string SportFamily, string RendererKind);

    private static async Task<Dictionary<string, CompetitionMap>> SeedCompetitionsAsync(
        SportsPlatformDbContext context, string path, IReadOnlyList<Row> rows,
        IReadOnlyDictionary<string, Guid> tenants, IReadOnlyDictionary<string, Sport> sports,
        TimeSpan anchorOffset,
        CancellationToken cancellationToken)
    {
        var stageRows = Read(path, "08_stages.csv");
        var groupRows = Read(path, "09_groups.csv");
        var standingRows = Read(path, "13_standings.csv");
        var contestRows = Read(path, "10_contests.csv");
        var presentationRows = Read(path, "18_presentation_metadata.csv")
            .ToDictionary(row => row["competition_id"], StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, CompetitionMap>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var sport = sports[row["sport_id"]];
            var competition = new Competition(StableId(row["competition_id"]), tenants[row["tenant_id"]], row["name"], sport.Id);
            if (presentationRows.TryGetValue(row["competition_id"], out var presentationRow))
                competition.UpdatePresentationConfiguration(
                    CompetitionPresentationConfiguration(presentationRow));
            var ruleset = sport.Versions.Where(item => item.IsPublished);
            if (row["sport_id"] is "rugby7" or "rugby15")
            {
                var variant = row["sport_id"] == "rugby7" ? RugbyVariants.Sevens : RugbyVariants.Fifteens;
                ruleset = ruleset.Where(item => RugbyVariants.FromValidationConfiguration(item.ValidationConfiguration) == variant);
            }
            var rulesetId = ruleset.OrderByDescending(item => item.VersionNumber).Select(item => (Guid?)item.Id).FirstOrDefault();
            if (rulesetId.HasValue) competition.AssignRulesetVersion(rulesetId.Value);
            competition.Publish();
            if (row["status"] == "COMPLETED") competition.Close();
            var dates = contestRows.Where(item => item["competition_id"] == row["competition_id"])
                .Select(item => ParseDate(item["start_at"], anchorOffset)).ToArray();
            var edition = competition.AddEdition(row["season_label"], dates.Min().AddDays(-7), dates.Max().AddDays(7));
            if (row["status"] == "COMPLETED") edition.Close(); else edition.Open();
            var stages = new Dictionary<string, Stage>(StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
            foreach (var stageRow in stageRows.Where(item => item["competition_id"] == row["competition_id"]).OrderBy(item => Int(item["display_order"])))
            {
                var stage = edition.AddStage(stageRow["name"], Stage(stageRow["stage_type"]), Int(stageRow["display_order"]));
                if (row["status"] == "COMPLETED") stage.Complete(); else stage.Open();
                stages[stageRow["stage_id"]] = stage;
                foreach (var groupRow in groupRows.Where(item => item["stage_id"] == stageRow["stage_id"]).OrderBy(item => Int(item["display_order"])))
                    groups[groupRow["group_id"]] = stage.AddGroup(groupRow["name"]);
                foreach (var standingRow in standingRows.Where(item =>
                             item["stage_id"] == stageRow["stage_id"]
                             && string.IsNullOrWhiteSpace(item["group_id"])))
                    groups[$"standing:{standingRow["standing_id"]}"] = stage.AddGroup(standingRow["name"]);
            }
            context.Competitions.Add(competition);
            result[row["competition_id"]] = new(competition, stages, groups, row["sport_id"], row["renderer_kind"]);
        }
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task<Dictionary<string, Contest>> SeedContestsAsync(
        SportsPlatformDbContext context, string path, IReadOnlyDictionary<string, CompetitionMap> competitions,
        IReadOnlyDictionary<string, Participant> participants, IReadOnlyDictionary<string, Sport> sports,
        TimeSpan anchorOffset,
        CancellationToken cancellationToken)
    {
        var rows = Read(path, "10_contests.csv");
        var participantRows = Read(path, "11_contest_participants.csv");
        var eventRows = Read(path, "12_contest_events.csv");
        var seriesRows = Read(path, "17_series.csv");
        var venues = Read(path, "03_venues.csv").ToDictionary(row => row["venue_id"], row => row["name"], StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, Contest>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var map = competitions[row["competition_id"]];
            var contestParticipants = participantRows.Where(item => item["contest_id"] == row["contest_id"]).OrderBy(item => Int(item["display_order"])).ToArray();
            var contest = new Contest(StableId(row["contest_id"]), map.Competition.TenantId,
                sports[map.SportFamily].Id, Shape(map.RendererKind, map.SportFamily));
            var names = contestParticipants.Select(item => participants[item["participant_id"]].Name).ToArray();
            contest.SetName(NonEmpty(row["summary"], names.Length == 2 ? $"{names[0]} vs {names[1]}" : $"{map.Competition.Name} — {row["round_label"]}"));
            if (map.Competition.RulesetVersionId.HasValue) contest.AssignRulesetVersion(map.Competition.RulesetVersionId.Value);
            contest.AssignToStage(map.Stages[row["stage_id"]].Id, result.Count(item => item.Value.StageId == map.Stages[row["stage_id"]].Id) + 1);
            if (!string.IsNullOrWhiteSpace(row["group_id"]) && map.Groups.TryGetValue(row["group_id"], out var group)) contest.AssignToGroup(group.Id);
            foreach (var item in contestParticipants)
                contest.AddParticipant(participants[item["participant_id"]].Id,
                    participants[item["participant_id"]] is Team ? "Team" : "Athlete", Int(item["display_order"]));
            var participantKeys = contestParticipants.Select(item => item["participant_id"])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var series = seriesRows.FirstOrDefault(item => item["competition_id"] == row["competition_id"]
                && item["stage_id"] == row["stage_id"]
                && participantKeys.SetEquals([item["participant_a_id"], item["participant_b_id"]]));
            if (series is not null)
            {
                var gameNumber = result.Values.Count(item => item.SeriesId == StableId(series["series_id"])) + 1;
                contest.AssignToSeries(StableId(series["series_id"]), gameNumber, Int(series["best_of"]));
            }
            contest.Schedule(venues.GetValueOrDefault(row["venue_id"], "Public demo venue"), ParseDate(row["start_at"], anchorOffset), 120);
            RecordPresentationMetric(contest, "periodLabel", row["period_label"]);
            RecordPresentationMetric(contest, "clock", row["clock"]);
            RecordPresentationMetric(contest, "summary", row["summary"]);
            RecordPresentationMetric(contest, "stageLabel", row["round_label"]);
            ApplyStatus(contest, row["status"]);
            var contestEventRows = eventRows.Where(item => item["contest_id"] == row["contest_id"])
                .OrderBy(item => Long(item["sequence"]))
                .ToArray();
            var segments = contestEventRows
                .Select(item => item["segment"])
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((code, index) => new
                {
                    Code = code,
                    Segment = contest.AddSegment(SegmentName(code), SegmentType(code), index + 1)
                })
                .ToDictionary(item => item.Code, item => item.Segment, StringComparer.OrdinalIgnoreCase);
            foreach (var eventRow in contestEventRows)
            {
                var eventParticipant = participants[eventRow["participant_id"]].Id;
                var segmentId = segments.GetValueOrDefault(eventRow["segment"])?.Id;
                var definition = TimelineDefinition(map.SportFamily, eventRow["event_type"]);
                var eventValue = string.IsNullOrWhiteSpace(eventRow["value"])
                    ? definition.Value.ToString(CultureInfo.InvariantCulture)
                    : eventRow["value"];
                var contestEvent = contest.AddEvent(eventRow["event_type"], eventParticipant, segmentId,
                    eventValue, null, "csv-seed", Long(eventRow["sequence"]), eventRow["event_id"]);
                contestEvent.Approve();
                var liveEvent = new LiveScore(
                    StableId($"timeline:{eventRow["event_id"]}"), contest.Id, contestEvent.Id,
                    eventParticipant, definition.Metric, Decimal(eventValue), segmentId,
                    ParseClock(eventRow["clock"]), contest.TenantId, eventRow["event_type"],
                    deviceId: "csv-timeline", idempotencyKey: eventRow["event_id"],
                    rulesetVersionId: contest.RulesetVersionId);
                liveEvent.SetSequence(checked((int)Long(eventRow["sequence"])));
                liveEvent.SetPlayers(
                    ParticipantId(participants, eventRow["player_id"]),
                    ParticipantId(participants, eventRow["secondary_player_id"]));
                if (!string.IsNullOrWhiteSpace(eventRow["play_id"])
                    || !string.IsNullOrWhiteSpace(eventRow["description"]))
                    liveEvent.SetMetadata(JsonSerializer.Serialize(new
                    {
                        playId = EmptyToNull(eventRow["play_id"]),
                        description = EmptyToNull(eventRow["description"])
                    }));
                context.LiveScores.Add(liveEvent);
            }
            context.Contests.Add(contest);
            result[row["contest_id"]] = contest;
            AddScores(context, contest, contestParticipants, participants, row["status"],
                contestEventRows.Length > 0);
        }
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static void AddScores(SportsPlatformDbContext context, Contest contest,
        IReadOnlyList<Row> rows, IReadOnlyDictionary<string, Participant> participants, string status,
        bool hasTimelineEvents)
    {
        var scored = rows.Where(row => !string.IsNullOrWhiteSpace(row["score"])).ToArray();
        for (var index = 0; index < scored.Length; index++)
        {
            var row = scored[index];
            var participantId = participants[row["participant_id"]].Id;
            if (status is "APPROVED" or "CORRECTED")
            {
                var score = Decimal(row["score"]);
                var result = new ContestResult(StableId($"result:{contest.Id}:{participantId}"), contest.Id,
                    StableId($"result-event:{contest.Id}"), participantId);
                result.SetScore(score, row["score"], null);
                var rank = string.IsNullOrWhiteSpace(row["result_position"]) ? index + 1 : Int(row["result_position"]);
                result.SetRank(rank, rank.ToString(CultureInfo.InvariantCulture));
                result.Submit(); result.BeginReview(); result.Approve(SystemAdministratorId); result.Publish();
                context.ContestResults.Add(result);
            }
            if (!hasTimelineEvents && status is ("IN_PROGRESS" or "SUSPENDED" or "PAUSED"))
            {
                var live = new LiveScore(StableId($"live:{contest.Id}:{participantId}"), contest.Id,
                    StableId($"live-event:{contest.Id}:{participantId}"), participantId, "score", Decimal(row["score"]),
                    tenantId: contest.TenantId, eventType: "SCORE", deviceId: "csv-seed");
                live.SetDisplayValue(row["score"]);
                live.SetSequence(index + 1);
                context.LiveScores.Add(live);
            }
        }
    }

    private static async Task SeedStandingsAsync(SportsPlatformDbContext context, string path,
        IReadOnlyDictionary<string, CompetitionMap> competitions, IReadOnlyDictionary<string, Participant> participants,
        CancellationToken cancellationToken)
    {
        var definitions = Read(path, "13_standings.csv").ToDictionary(row => row["standing_id"], StringComparer.OrdinalIgnoreCase);
        foreach (var row in Read(path, "14_standing_rows.csv"))
        {
            var definition = definitions[row["standing_id"]];
            var map = competitions[definition["competition_id"]];
            Guid groupId;
            if (!string.IsNullOrWhiteSpace(definition["group_id"]) && map.Groups.TryGetValue(definition["group_id"], out var group)) groupId = group.Id;
            else
                groupId = map.Groups[$"standing:{definition["standing_id"]}"].Id;
            var standing = new Standing(StableId($"{row["standing_id"]}:{row["participant_id"]}"),
                map.Competition.Id, groupId, participants[row["participant_id"]].Id);
            standing.UpdateStats(Int(row["played"]), Int(row["won"]), Int(row["drawn"]), Int(row["lost"]),
                Int(row["points_for"]), Int(row["points_against"]), Decimal(row["points"]));
            standing.SetPosition(Int(row["position"]));
            standing.SetMetadata(JsonSerializer.Serialize(new
            {
                form = row["form"],
                bonusPoints = Int(row["bonus_points"]),
                qualificationCode = row["qualification_code"]
            }));
            context.Standings.Add(standing);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedLeaderboardsAsync(SportsPlatformDbContext context, string path,
        IReadOnlyDictionary<string, CompetitionMap> competitions, IReadOnlyDictionary<string, Contest> contests,
        IReadOnlyDictionary<string, Participant> participants, CancellationToken cancellationToken)
    {
        foreach (var grouping in Read(path, "15_leaderboard_entries.csv").GroupBy(row => row["contest_id"]))
        {
            var leaderboard = new Leaderboard(StableId($"leaderboard:{grouping.Key}"), contests[grouping.Key].Id,
                $"{competitions[grouping.First()["competition_id"]].Competition.Name} Leaderboard", LeaderboardType.Overall);
            foreach (var row in grouping)
            {
                var entry = new LeaderboardEntry(StableId($"leaderboard:{grouping.Key}:{row["participant_id"]}"),
                    leaderboard.Id, participants[row["participant_id"]].Id, Int(row["position"]), Decimal(row["total"]));
                entry.UpdateRank(Int(row["position"]), Decimal(row["total"]), row["total"]);
                leaderboard.AddEntry(entry);
            }
            context.Leaderboards.Add(leaderboard);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBracketsAsync(SportsPlatformDbContext context, string path,
        IReadOnlyDictionary<string, CompetitionMap> competitions, IReadOnlyDictionary<string, Contest> contests,
        IReadOnlyDictionary<string, Participant> participants, CancellationToken cancellationToken)
    {
        var participantRows = Read(path, "11_contest_participants.csv");
        foreach (var competitionRows in Read(path, "16_bracket_slots.csv").GroupBy(row => row["competition_id"]))
        {
            var rows = competitionRows.ToArray();
            var bracket = new Bracket(StableId($"bracket:{competitionRows.Key}"), competitions[competitionRows.Key].Competition.Id,
                $"{competitions[competitionRows.Key].Competition.Name} Bracket", BracketType.SingleElimination,
                Math.Max(2, rows.Length * 2));
            bracket.SetSeeded(true); bracket.SetDrawSeed(20260813);
            var matches = new Dictionary<string, BracketMatch>(StringComparer.OrdinalIgnoreCase);
            var roundNumber = 0;
            foreach (var stageRows in rows.GroupBy(row => row["stage_id"]))
            {
                roundNumber++;
                var round = new BracketRound(StableId($"bracket-round:{competitionRows.Key}:{stageRows.Key}"), bracket.Id,
                    roundNumber, competitions[competitionRows.Key].Stages[stageRows.Key].Name, stageRows.Count());
                var matchNumber = 0;
                foreach (var row in stageRows)
                {
                    matchNumber++;
                    var match = new BracketMatch(StableId($"bracket-match:{competitionRows.Key}:{row["bracket_slot"]}"), round.Id, matchNumber);
                    var cps = participantRows.Where(item => item["contest_id"] == row["contest_id"]).OrderBy(item => Int(item["display_order"])).ToArray();
                    match.SetParticipants(cps.ElementAtOrDefault(0) is { } first ? participants[first["participant_id"]].Id : null,
                        cps.ElementAtOrDefault(1) is { } second ? participants[second["participant_id"]].Id : null);
                    match.BindContest(contests[row["contest_id"]].Id);
                    if (row["status"] == "COMPLETE" && cps.FirstOrDefault(item => Bool(item["winner"])) is { } winner)
                        match.SetWinner(participants[winner["participant_id"]].Id);
                    round.AddMatch(match); matches[row["bracket_slot"]] = match;
                }
                bracket.AddRound(round);
            }
            foreach (var row in rows.Where(row => !string.IsNullOrWhiteSpace(row["next_slot"])))
                if (matches.TryGetValue(row["next_slot"], out var next)) matches[row["bracket_slot"]].AdvanceTo(next.Id, 1);
            bracket.Publish(); bracket.Start(); context.Brackets.Add(bracket);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSponsorsAsync(SportsPlatformDbContext context, string path,
        IReadOnlyDictionary<string, Guid> tenants, CancellationToken cancellationToken)
    {
        foreach (var row in Read(path, "19_sponsors.csv"))
        {
            var sponsor = new Sponsor(StableId(row["sponsor_id"]), tenants[row["tenant_id"]], row["name"]);
            sponsor.SetLogoUrl(EmptyToNull(row["logo_url"])); sponsor.SetWebsite(EmptyToNull(row["website_url"]));
            context.Sponsors.Add(sponsor);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyStatus(Contest contest, string status)
    {
        switch (status)
        {
            case "STARTING_SOON": contest.OpenCheckIn(); contest.MarkReady(); break;
            case "READY": contest.OpenCheckIn(); contest.MarkReady(); break;
            case "IN_PROGRESS": contest.Start(); break;
            case "PAUSED": contest.Start(); contest.Pause(); break;
            case "SUSPENDED": contest.Start(); contest.Suspend(); break;
            case "PENDING_APPROVAL": contest.Start(); contest.Complete(); contest.SubmitForApproval(); break;
            case "APPROVED": contest.Start(); contest.Complete(); contest.SubmitForApproval(); contest.Approve(); contest.TransitionTo(ContestStatus.Published); break;
            case "CORRECTED": contest.Start(); contest.Complete(); contest.SubmitForApproval(); contest.Approve(); contest.MarkCorrected(); break;
            case "ABANDONED": contest.Start(); contest.Abandon(); break;
            case "POSTPONED": contest.Postpone("Postponed in the public staging scenario matrix."); break;
            case "CANCELLED": contest.Cancel(); break;
        }
    }

    private static async Task ValidateImportedDataAsync(SportsPlatformDbContext context, Guid primaryTenantId,
        CancellationToken cancellationToken)
    {
        var competitionCount = await context.Competitions.CountAsync(item => item.TenantId == primaryTenantId, cancellationToken);
        var participantCount = await context.Participants.CountAsync(item => item.TenantId == primaryTenantId, cancellationToken);
        var contestCount = await context.Contests.CountAsync(item => item.TenantId == primaryTenantId, cancellationToken);
        if (competitionCount < 18 || participantCount < 140 || contestCount < 130)
            throw new InvalidOperationException($"CSV staging seed validation failed: competitions={competitionCount}, participants={participantCount}, contests={contestCount}.");
        if (!await context.Contests.AnyAsync(item => item.TenantId == primaryTenantId && item.Status == ContestStatus.InProgress, cancellationToken))
            throw new InvalidOperationException("CSV staging seed validation failed: no live contest was imported.");
        var sportCount = await context.TenantSports
            .Where(item => item.TenantId == primaryTenantId)
            .Select(item => item.SportId)
            .Distinct()
            .CountAsync(cancellationToken);
        var timelineSportCount = await context.LiveScores
            .Where(item => item.TenantId == primaryTenantId && item.DeviceId == "csv-timeline")
            .Join(context.Contests, score => score.ContestId, contest => contest.Id,
                (score, contest) => contest.SportId)
            .Distinct()
            .CountAsync(cancellationToken);
        if (sportCount < 12 || timelineSportCount < 12)
            throw new InvalidOperationException(
                $"CSV staging seed coverage failed: sports={sportCount}, timelineSports={timelineSportCount}.");
        if (!await context.Contests.AnyAsync(item => item.TenantId == primaryTenantId
                && item.CanonicalStatus == CanonicalContestStatus.Postponed, cancellationToken)
            || !await context.Contests.AnyAsync(item => item.TenantId == primaryTenantId
                && item.Status == ContestStatus.Corrected, cancellationToken))
            throw new InvalidOperationException(
                "CSV staging seed coverage failed: postponed and corrected scenarios are required.");
    }

    private static void ValidatePack(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException($"Public seed CSV pack was not found at '{path}'.");
        for (var index = 1; index <= 19; index++)
            if (!Directory.EnumerateFiles(path, $"{index:D2}_*.csv").Any())
                throw new InvalidOperationException($"Public seed CSV pack is missing file {index:D2}.");
    }

    private static void ValidatePackCoverage(string path)
    {
        var competitions = Read(path, "07_competitions.csv");
        var contests = Read(path, "10_contests.csv");
        var events = Read(path, "12_contest_events.csv");
        var requiredSports = SportCodes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seededSports = competitions.Select(item => item["sport_id"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingSports = requiredSports.Except(seededSports, StringComparer.OrdinalIgnoreCase).ToArray();
        if (missingSports.Length > 0)
            throw new InvalidOperationException(
                $"Public seed CSV pack has no competitions for: {string.Join(", ", missingSports)}.");
        if (competitions.Select(item => item["tenant_id"])
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
            throw new InvalidOperationException(
                "Public seed CSV pack needs an independent competition in the isolation tenant.");
        var requiredFormats = new[]
        {
            "DOUBLE_ROUND_ROBIN", "SINGLE_ELIMINATION", "GROUP_PLUS_KNOCKOUT",
            "BEST_OF_3", "MULTI_ROUND_LEADERBOARD", "HEAT_TO_FINAL", "FIGHT_CARD",
            "POOL_PLUS_KNOCKOUT"
        };
        var formats = competitions.Select(item => item["format_code"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requiredFormats.Any(format => !formats.Contains(format)))
            throw new InvalidOperationException("Public seed CSV pack is missing a required competition format.");
        var eventCompetitionIds = events.Select(item => item["contest_id"])
            .Join(contests, id => id, contest => contest["contest_id"],
                (_, contest) => contest["competition_id"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eventSports = competitions.Where(item => eventCompetitionIds.Contains(item["competition_id"]))
            .Select(item => item["sport_id"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingTimelineSports = requiredSports
            .Except(eventSports, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingTimelineSports.Length > 0)
            throw new InvalidOperationException(
                $"Public seed CSV pack has no timeline for: {string.Join(", ", missingTimelineSports)}.");
    }

    private static List<Row> Read(string path, string file)
    {
        using var parser = new TextFieldParser(Path.Combine(path, file), Encoding.UTF8)
        { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields() ?? throw new InvalidOperationException($"{file} has no header.");
        var rows = new List<Row>();
        while (!parser.EndOfData)
        {
            var values = parser.ReadFields() ?? [];
            if (values.Length != headers.Length) throw new InvalidOperationException($"{file} contains a malformed row.");
            rows.Add(new Row(headers.Zip(values).ToDictionary(item => item.First, item => item.Second, StringComparer.OrdinalIgnoreCase)));
        }
        return rows;
    }

    private static StageType Stage(string value) => value switch
    {
        "GROUP" => StageType.Group, "KNOCKOUT" => StageType.Knockout,
        "LEAGUE" => StageType.League, "LEADERBOARD" => StageType.Leaderboard,
        "FIGHT_CARD" => StageType.FightCard, "MEET" => StageType.Heat,
        "SERIES" => StageType.Knockout, "PLACEMENT" => StageType.Knockout,
        _ => StageType.Group
    };

    private static string Shape(string renderer, string sport) => renderer switch
    {
        "MULTI_PARTICIPANT" => "MultiRoundLeaderboard", "RACE_HEAT" => "Heat",
        "COMBAT" => "Bout", "COMPLEX_SCORE" => "CricketMatch",
        _ when sport == "tennis" => "IndividualMatch", _ => "TeamVsTeam"
    };

    private static string CompetitionPresentationConfiguration(Row row)
    {
        var renderer = row["renderer_kind"] switch
        {
            "MULTI_PARTICIPANT" => "multi-participant-leaderboard",
            "RACE_HEAT" => "race-heat",
            "COMBAT" => "fight-card-bout",
            "COMPLEX_SCORE" => "cricket-scorecard",
            _ => "head-to-head"
        };
        var sourceViews = JsonSerializer.Deserialize<string[]>(row["views_json"]) ?? [];
        var normalizedTabs = sourceViews.Select(view => view.ToLowerInvariant() switch
            {
                "games" or "matches" or "schedule" => "fixtures",
                "table" => "standings",
                "teams" or "players" or "athletes" or "fighters" => "participants",
                "playoffs" or "knockout" => "bracket",
                _ => view.ToLowerInvariant()
            })
            .Where(view => view is "overview" or "fixtures" or "results" or "standings"
                or "groups" or "bracket" or "series" or "leaderboard"
                or "medals" or "participants" or "statistics")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sourcePresentation = string.IsNullOrWhiteSpace(row["presentation_json"])
            ? new JsonObject()
            : JsonNode.Parse(row["presentation_json"])?.AsObject() ?? new JsonObject();
        sourcePresentation["primaryContestRenderer"] = renderer;
        sourcePresentation["scoreVariant"] = row["score_variant"];
        sourcePresentation["defaultTab"] = normalizedTabs.Contains(row["default_view"], StringComparer.OrdinalIgnoreCase)
            ? row["default_view"].ToLowerInvariant()
            : "overview";
        sourcePresentation["visibleTabs"] = new JsonArray(
            normalizedTabs.Select(item => (JsonNode?)JsonValue.Create(item)).ToArray());
        sourcePresentation["bracketEnabled"] = sourceViews.Any(view =>
            view is "bracket" or "knockout" or "playoffs" or "series");
        sourcePresentation["seriesEnabled"] = sourceViews.Any(view =>
            view is "series" or "playoffs");
        sourcePresentation["medalTableEnabled"] = sourceViews.Contains(
            "medals", StringComparer.OrdinalIgnoreCase);
        return sourcePresentation.ToJsonString();
    }

    private static void RecordPresentationMetric(Contest contest, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        contest.RecordMetric($"presentation.{name}", null, value, dataType: "String");
    }

    private static async Task InsertPresentationMetricIfMissingAsync(
        SportsPlatformDbContext context, Guid tenantId, Guid contestId,
        string name, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var metricName = $"presentation.{name}";
        var metricId = StableId($"metric:{contestId:N}:{metricName}");
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ContestMetrics"
                ("Id", "TenantId", "ContestId", "ParticipantId", "SegmentId",
                 "Name", "NumericValue", "TextValue", "Unit", "DataType",
                 "Precision", "RecordedAt", "CreatedAt", "UpdatedAt")
            SELECT {metricId}, {tenantId}, {contestId}, NULL, NULL,
                   {metricName}, NULL, {value.Trim()}, NULL, 'String', 6,
                   NOW(), NOW(), NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM "ContestMetrics"
                WHERE "ContestId" = {contestId} AND "Name" = {metricName})
            """, cancellationToken);
    }

    private static DateTime ParseDate(string value, TimeSpan anchorOffset) => DateTime.SpecifyKind(
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces)
            .Add(anchorOffset), DateTimeKind.Utc);
    private static TimeSpan ResolveAnchorOffset(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "manifest.json")));
        var anchor = DateTime.Parse(document.RootElement.GetProperty("anchor_date").GetString()!,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).Date;
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(
            document.RootElement.GetProperty("timezone").GetString()!);
        var tenantToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone).Date;
        return tenantToday - anchor;
    }
    private static (string Metric, decimal Value) TimelineDefinition(string sport, string eventType)
    {
        if (sport == "football" && FootballTimelinePackage.Find(eventType) is { } football)
            return (football.RequiredMetric ?? "event", football.DefaultValue);
        if (sport == "basketball" && BasketballTimelinePackage.Find(eventType) is { } basketball)
            return (basketball.RequiredMetric ?? "event", basketball.DefaultValue);
        if (sport is "rugby7" or "rugby15"
            && RugbyTimelinePackage.Find(eventType,
                sport == "rugby7" ? RugbyVariants.Sevens : RugbyVariants.Fifteens) is { } rugby)
            return (rugby.RequiredMetric ?? "event", rugby.DefaultValue);
        var normalized = eventType.ToUpperInvariant();
        var scoring = normalized.Contains("SCORE", StringComparison.Ordinal)
            || normalized is "GOAL" or "RUN" or "FOUR" or "SIX" or "SET_WIN";
        return (scoring ? "score" : normalized.ToLowerInvariant(), 1);
    }
    private static Guid? ParticipantId(IReadOnlyDictionary<string, Participant> participants, string key) =>
        string.IsNullOrWhiteSpace(key) ? null : participants.GetValueOrDefault(key)?.Id;
    private static decimal? ParseClock(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().TrimEnd('\'');
        if (TimeSpan.TryParseExact(trimmed, @"mm\:ss", CultureInfo.InvariantCulture, out var time))
            return (decimal)time.TotalSeconds;
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var minute)
            ? minute * 60
            : null;
    }
    private static string SegmentName(string code) => code.ToUpperInvariant() switch
    {
        "1H" => "First Half", "2H" => "Second Half",
        "Q1" => "First Quarter", "Q2" => "Second Quarter",
        "Q3" => "Third Quarter", "Q4" => "Fourth Quarter",
        "R1" => "Round 1", "R2" => "Round 2", "R3" => "Round 3",
        _ => code.Replace('_', ' ')
    };
    private static string SegmentType(string code) => code.ToUpperInvariant() switch
    {
        "1H" or "2H" => "Half",
        "Q1" or "Q2" or "Q3" or "Q4" => "Quarter",
        var value when value.StartsWith("SET", StringComparison.Ordinal) => "Set",
        var value when value.StartsWith("R", StringComparison.Ordinal) => "Round",
        var value when value.Contains("INNINGS", StringComparison.Ordinal) => "Innings",
        _ => "Period"
    };
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"public-csv-seed:{value}"))[..16]);
    private static int Int(string value) => int.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static long Long(string value) => long.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static decimal Decimal(string value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static bool Bool(string value) => bool.TryParse(value, out var result) && result;
    private static string NonEmpty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Initials(string value) => string.Concat(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4).Select(word => char.ToUpperInvariant(word[0])));

    private sealed class Row(Dictionary<string, string> values)
    {
        public string this[string key] => values.GetValueOrDefault(key, string.Empty);
    }
}
