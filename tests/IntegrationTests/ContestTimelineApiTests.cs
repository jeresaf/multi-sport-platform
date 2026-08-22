using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Sports.Services;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class ContestTimelineApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ContestTimelineApiTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_timeline_is_canonical_deduplicated_versioned_and_tenant_scoped()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var contestId = Guid.NewGuid();
        Guid reversedEventId;

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var participants = await database.Participants
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id)
                .Take(2)
                .ToArrayAsync();
            participants.Should().HaveCount(2);

            var contest = new Contest(contestId, tenantId, sportId, "TeamVsTeam");
            contest.SetName("Canonical timeline test");
            contest.AddParticipant(participants[0], "Team");
            contest.AddParticipant(participants[1], "Team");
            contest.Schedule("Timeline Stadium", DateTime.UtcNow.AddHours(1), 90);
            var legacyGoal = contest.AddEvent(
                "GOAL", participants[0], null, "1", null,
                "legacy", 1, $"legacy-{contestId:N}-goal");
            var possession = contest.AddEvent(
                "POSSESSION", participants[1], null, "54", null,
                "legacy", 2, $"legacy-{contestId:N}-possession");
            possession.Approve();
            database.Contests.Add(contest);

            var goal = new LiveScore(
                Guid.NewGuid(), contestId, legacyGoal.Id, participants[0], "score", 1,
                tenantId: tenantId, eventType: "GOAL", deviceId: "scorer-a",
                idempotencyKey: $"score-{contestId:N}-goal");
            goal.SetSequence(1);
            reversedEventId = Guid.NewGuid();
            var card = new LiveScore(
                Guid.NewGuid(), contestId, reversedEventId, participants[1], "cards", 1,
                tenantId: tenantId, eventType: "YELLOW_CARD", deviceId: "scorer-a",
                idempotencyKey: $"score-{contestId:N}-card");
            card.SetSequence(2);
            var shootout = new LiveScore(
                Guid.NewGuid(), contestId, Guid.NewGuid(), participants[0],
                "shootout_score", 1, tenantId: tenantId,
                eventType: "PENALTY_SHOOTOUT_SCORE", deviceId: "scorer-a",
                idempotencyKey: $"score-{contestId:N}-shootout");
            shootout.SetSequence(3);
            database.LiveScores.AddRange(goal, card, shootout);
            await database.SaveChangesAsync();
            contest.TimelineVersion.Should().Be(4);
        }

        var route = $"/api/public/tenants/premier-league/contests/{contestId}/timeline";
        using (var response = await client.GetAsync(route))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("contestId").GetGuid().Should().Be(contestId);
            body.RootElement.GetProperty("timelineVersion").GetInt64().Should().Be(4);
            var events = body.RootElement.GetProperty("events").EnumerateArray().ToArray();
            events.Should().HaveCount(4, "the matching legacy and live score IDs are one event");
            events.Select(item => item.GetProperty("sequence").GetInt64())
                .Should().Equal(1, 2, 3, 4);
            events.Select(item => item.GetProperty("category").GetString())
                .Should().Contain(new[] { "SCORING", "DISCIPLINE", "STATISTIC" });
            events.Should().OnlyContain(item =>
                item.GetProperty("status").GetString() == "CONFIRMED");
            var goalEvent = events.Single(item => item.GetProperty("type").GetString() == "GOAL");
            goalEvent.GetProperty("primaryParticipantName").GetString()
                .Should().NotBeNullOrWhiteSpace();
            goalEvent.GetProperty("homeScoreAfter").GetDecimal().Should().Be(1);
            goalEvent.GetProperty("awayScoreAfter").GetDecimal().Should().Be(0);
            var shootoutEvent = events.Single(item =>
                item.GetProperty("type").GetString() == "PENALTY_SHOOTOUT_SCORE");
            shootoutEvent.GetProperty("homeScoreAfter").GetDecimal().Should().Be(1);
            shootoutEvent.GetProperty("shootoutHomeScoreAfter").GetDecimal().Should().Be(1);
            shootoutEvent.GetProperty("shootoutAwayScoreAfter").GetDecimal().Should().Be(0);
        }

        using (var keyResponse = await client.GetAsync($"{route}?mode=key"))
        using (var keyBody = JsonDocument.Parse(await keyResponse.Content.ReadAsStringAsync()))
            keyBody.RootElement.GetProperty("events").GetArrayLength().Should().Be(3);

        (await client.GetAsync(
            $"/api/public/tenants/nba/contests/{contestId}/timeline"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"{route}?afterVersion=99"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var score = await database.LiveScores.SingleAsync(item =>
                item.ContestId == contestId && item.EventId == reversedEventId);
            score.UpdateValue(0, "0");
            database.LiveScoreUpdates.Add(new LiveScoreUpdate(
                Guid.NewGuid(), score.Id, "Reversed: incorrect card", "1", "0", "tester"));
            await database.SaveChangesAsync();
        }

        using (var response = await client.GetAsync(route))
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            body.RootElement.GetProperty("timelineVersion").GetInt64().Should().Be(5);
            body.RootElement.GetProperty("events").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == reversedEventId)
                .GetProperty("status").GetString().Should().Be("REVERSED");
        }

        using (var unchanged = await client.GetAsync($"{route}?afterVersion=5"))
        using (var body = JsonDocument.Parse(await unchanged.Content.ReadAsStringAsync()))
            body.RootElement.GetProperty("events").GetArrayLength().Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Contests.RemoveRange(database.Contests.Where(item => item.Id == contestId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Rugby_sevens_timeline_uses_assigned_variant_and_canonical_score_effects()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var rugbySportId = Guid.Parse("00000000-0000-0000-0000-000000000106");
        var contestId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var participants = await database.Participants
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id)
                .Take(2)
                .ToArrayAsync();
            var rugbyRulesets = await database.Sports.AsNoTracking()
                .Where(item => item.Id == rugbySportId)
                .SelectMany(item => item.Versions)
                .Where(item => item.IsPublished)
                .ToArrayAsync();
            var sevensRulesetId = rugbyRulesets
                .Where(item => RugbyVariants.FromValidationConfiguration(
                    item.ValidationConfiguration) == RugbyVariants.Sevens)
                .Select(item => item.Id)
                .First();

            var contest = new Contest(contestId, tenantId, rugbySportId, "TeamVsTeam");
            contest.SetName("Rugby sevens timeline test");
            contest.AssignRulesetVersion(sevensRulesetId);
            contest.AddParticipant(participants[0], "Team");
            contest.AddParticipant(participants[1], "Team");
            contest.Schedule("Sevens Stadium", DateTime.UtcNow.AddHours(1), 20);
            database.Contests.Add(contest);

            var tryScore = new LiveScore(
                Guid.NewGuid(), contestId, Guid.NewGuid(), participants[0], "score", 5,
                tenantId: tenantId, eventType: "TRY", deviceId: "rugby-scorer",
                idempotencyKey: $"score-{contestId:N}-try");
            tryScore.SetSequence(1);
            var conversion = new LiveScore(
                Guid.NewGuid(), contestId, Guid.NewGuid(), participants[0], "score", 2,
                tenantId: tenantId, eventType: "CONVERSION", deviceId: "rugby-scorer",
                idempotencyKey: $"score-{contestId:N}-conversion");
            conversion.SetSequence(2);
            database.LiveScores.AddRange(tryScore, conversion);
            await database.SaveChangesAsync();
        }

        var route = $"/api/public/tenants/premier-league/contests/{contestId}/timeline";
        using (var response = await client.GetAsync(route))
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var events = body.RootElement.GetProperty("events").EnumerateArray().ToArray();
            events.Should().HaveCount(2);
            events.Should().OnlyContain(item =>
                item.GetProperty("sportVariant").GetString() == "RUGBY_7");
            events.Select(item => item.GetProperty("type").GetString())
                .Should().Equal("TRY", "CONVERSION_MADE");
            events[0].GetProperty("homeScoreAfter").GetDecimal().Should().Be(5);
            events[1].GetProperty("homeScoreAfter").GetDecimal().Should().Be(7);
        }

        using var cleanupScope = _factory.Services.CreateScope();
        var cleanup = cleanupScope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        cleanup.Contests.RemoveRange(cleanup.Contests.Where(item => item.Id == contestId));
        await cleanup.SaveChangesAsync();
    }

    [Fact]
    public async Task Public_match_statistics_exclude_lifecycle_and_adjustment_events()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var contestId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var participants = await database.Participants
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id)
                .Take(2)
                .ToArrayAsync();
            var contest = new Contest(contestId, tenantId, sportId, "TeamVsTeam");
            contest.SetName("Public statistics filtering test");
            contest.AddParticipant(participants[0], "Team");
            contest.AddParticipant(participants[1], "Team");
            contest.Schedule("Statistics Stadium", DateTime.UtcNow.AddHours(1), 90);
            foreach (var (type, participant, sequence) in new[]
            {
                ("MATCH_START", participants[0], 1L),
                ("PERIOD_END", participants[0], 2L),
                ("SCORE_ADJUSTMENT", participants[0], 3L),
                ("GOAL", participants[0], 4L),
                ("YELLOW_CARD", participants[1], 5L),
                ("FULL_TIME", participants[0], 6L)
            })
            {
                var item = contest.AddEvent(
                    type, participant, null, "1", null, "test", sequence,
                    $"statistics-{contestId:N}-{sequence}");
                item.Approve();
            }
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        using (var response = await client.GetAsync(
                   $"/api/public/tenants/premier-league/contests/{contestId}"))
        using (var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var statistics = body.RootElement.GetProperty("statistics");
            statistics.GetProperty("totalEvents").GetInt32().Should().Be(2);
            statistics.GetProperty("eventBreakdown").EnumerateArray()
                .Select(item => item.GetProperty("label").GetString())
                .Should().BeEquivalentTo("GOAL", "YELLOW_CARD");
            body.RootElement.GetProperty("availableTabs").EnumerateArray()
                .Select(item => item.GetString())
                .Should().Contain("statistics");
        }

        using var cleanupScope = _factory.Services.CreateScope();
        var cleanup = cleanupScope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        cleanup.Contests.RemoveRange(cleanup.Contests.Where(item => item.Id == contestId));
        await cleanup.SaveChangesAsync();
    }
}
