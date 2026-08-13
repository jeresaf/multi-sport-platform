using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;
using SportsPlatform.Api.Hubs;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.BackgroundJobs;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Reporting.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Subscriptions.Entities;
using SportsPlatform.Modules.Sponsorship.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class ApiWorkflowTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ApiWorkflowTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicShowcase_ExposesEverySportAndLifecycleScenario()
    {
        using var client = _factory.CreateClient();
        const string root = "/api/public/tenants/public-e2e-showcase";

        var home = await client.GetFromJsonAsync<JsonElement>($"{root}/home");
        home.GetProperty("tenant").GetProperty("slug").GetString()
            .Should().Be("public-e2e-showcase");
        home.GetProperty("liveContests").GetArrayLength().Should().Be(9);
        home.GetProperty("upcomingContests").GetArrayLength().Should().Be(18);

        var competitions = await client.GetFromJsonAsync<JsonElement>($"{root}/competitions");
        competitions.GetArrayLength().Should().Be(9);
        competitions.EnumerateArray()
            .Select(item => item.GetProperty("sportId").GetGuid())
            .Distinct().Should().HaveCount(7);

        var fixtures = await client.GetFromJsonAsync<JsonElement>($"{root}/fixtures?pageSize=100");
        var fixtureItems = fixtures.EnumerateArray().ToArray();
        fixtureItems.Should().HaveCount(36);
        var statusCounts = fixtureItems
            .GroupBy(item => item.GetProperty("status").GetString())
            .ToDictionary(group => group.Key!, group => group.Count());
        statusCounts[nameof(ContestStatus.Published)].Should().Be(9);
        statusCounts[nameof(ContestStatus.InProgress)].Should().Be(9);
        statusCounts[nameof(ContestStatus.Ready)].Should().Be(9);
        statusCounts[nameof(ContestStatus.Scheduled)].Should().Be(9);

        var golf = await client.GetAsync(
            $"{root}/golf/leaderboard?competitionId=80000000-0000-0000-0000-000000000104");
        golf.StatusCode.Should().Be(HttpStatusCode.OK);
        var golfPayload = await golf.Content.ReadFromJsonAsync<JsonElement>();
        golfPayload.GetProperty("entries").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task OperationalMetrics_ExposeEveryAuthoritativeDashboardSignal()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/public/tenants/premier-league");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/metrics");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var metrics = await response.Content.ReadAsStringAsync();

        metrics.Should().Contain("sportsplatform_api_up 1");
        metrics.Should().Contain("sportsplatform_dependency_health{dependency=\"postgresql\"}");
        metrics.Should().Contain("sportsplatform_dependency_health{dependency=\"redis\"}");
        metrics.Should().Contain("sportsplatform_signalr_connections{tenant_id=\"all\"}");
        metrics.Should().Contain("sportsplatform_queue_backlog{queue=\"reports\",tenant_id=\"all\"}");
        metrics.Should().Contain("sportsplatform_failed_jobs{queue=\"reports\",tenant_id=\"all\"}");
        metrics.Should().Contain("sportsplatform_notification_backlog{tenant_id=\"all\"}");
        metrics.Should().Contain("sportsplatform_notification_failures{tenant_id=\"all\"}");
        metrics.Should().Contain("# HELP sportsplatform_tenant_usage");
        metrics.Should().Contain("sportsplatform_http_requests_total{method=\"GET\",status_code=\"200\",tenant_id=\""
            + tenantId + "\"}");
        metrics.Should().NotContain("admin@sportsplatform.com");
    }

    [Fact]
    public async Task PublicTenantRoutes_AreAnonymousAndTenantScoped()
    {
        using var client = _factory.CreateClient();

        var tenant = await client.GetAsync("/api/public/tenants/premier-league");
        var home = await client.GetAsync("/api/public/tenants/premier-league/home");
        var fixtures = await client.GetAsync(
            "/api/public/tenants/premier-league/fixtures");
        var missing = await client.GetAsync("/api/public/tenants/not-a-tenant");
        var missingFixtures = await client.GetAsync(
            "/api/public/tenants/not-a-tenant/fixtures");

        tenant.StatusCode.Should().Be(HttpStatusCode.OK);
        home.StatusCode.Should().Be(HttpStatusCode.OK);
        fixtures.StatusCode.Should().Be(HttpStatusCode.OK);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingFixtures.StatusCode.Should().Be(HttpStatusCode.NotFound);
        home.Headers.CacheControl.Should().NotBeNull();
        home.Headers.CacheControl!.Public.Should().BeTrue();
        home.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(30));
        fixtures.Headers.CacheControl.Should().NotBeNull();
        fixtures.Headers.CacheControl!.Public.Should().BeTrue();

        using var payload = JsonDocument.Parse(await tenant.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("slug").GetString()
            .Should().Be("premier-league");
        using var homePayload = JsonDocument.Parse(await home.Content.ReadAsStringAsync());
        homePayload.RootElement.GetProperty("tenant").GetProperty("branding");
        homePayload.RootElement.GetProperty("featuredCompetitions").ValueKind
            .Should().Be(JsonValueKind.Array);
        homePayload.RootElement.GetProperty("liveContests").ValueKind
            .Should().Be(JsonValueKind.Array);
        homePayload.RootElement.GetProperty("upcomingContests").ValueKind
            .Should().Be(JsonValueKind.Array);
        homePayload.RootElement.GetProperty("recentResults").ValueKind
            .Should().Be(JsonValueKind.Array);
        homePayload.RootElement.GetProperty("sponsors").ValueKind
            .Should().Be(JsonValueKind.Array);
        using var fixturesPayload = JsonDocument.Parse(
            await fixtures.Content.ReadAsStringAsync());
        fixturesPayload.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        foreach (var fixture in fixturesPayload.RootElement.EnumerateArray())
        {
            fixture.GetProperty("competitionId").GetGuid()
                .Should().NotBe(Guid.Empty);
            fixture.GetProperty("sportCode").GetString()
                .Should().NotBeNullOrWhiteSpace();
            fixture.GetProperty("sportName").GetString()
                .Should().NotBeNullOrWhiteSpace();
            fixture.GetProperty("renderer").GetString()
                .Should().BeOneOf("head-to-head", "multi-participant-leaderboard",
                    "race-heat", "fight-card-bout", "cricket-scorecard");
            fixture.GetProperty("participants").ValueKind
                .Should().Be(JsonValueKind.Array);
            var presentation = fixture.GetProperty("presentation");
            presentation.ValueKind.Should().Be(JsonValueKind.Object);
            presentation.TryGetProperty("scoreVariant", out _).Should().BeTrue();
            presentation.TryGetProperty("periodLabel", out _).Should().BeTrue();
            presentation.TryGetProperty("clock", out _).Should().BeTrue();
            presentation.GetProperty("eventSummary").ValueKind
                .Should().Be(JsonValueKind.Array);
            foreach (var participant in fixture.GetProperty("participants").EnumerateArray())
            {
                participant.GetProperty("participantId").GetGuid()
                    .Should().NotBe(Guid.Empty);
                participant.GetProperty("name").GetString()
                    .Should().NotBeNullOrWhiteSpace();
                participant.TryGetProperty("scoreDisplay", out _).Should().BeTrue();
                participant.TryGetProperty("resultStatus", out _).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task PublicCompetitionDetail_IsTenantScopedConditionalAndSportLabelled()
    {
        using var client = _factory.CreateClient();

        var footballId = Guid.Parse(
            "00000000-0000-0000-0000-000000000300");
        var detailResponse = await client.GetAsync(
            $"/api/public/tenants/premier-league/competitions/{footballId}");
        detailResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await detailResponse.Content.ReadAsStringAsync());
        detailResponse.Headers.CacheControl.Should().NotBeNull();
        detailResponse.Headers.CacheControl!.Public.Should().BeTrue();
        using (var detail = JsonDocument.Parse(
                   await detailResponse.Content.ReadAsStringAsync()))
        {
            var tabs = detail.RootElement.GetProperty("availableTabs")
                .EnumerateArray().Select(item => item.GetString()).ToArray();
            detail.RootElement.GetProperty("presentation")
                .GetProperty("primaryContestRenderer").ValueKind
                .Should().Be(JsonValueKind.Null);
            detail.RootElement.GetProperty("sport").GetProperty("code")
                .GetString().Should().Be("FOOTBALL");
            detail.RootElement.TryGetProperty("region", out _).Should().BeTrue();
            detail.RootElement.GetProperty("sponsors").ValueKind
                .Should().Be(JsonValueKind.Array);
            detail.RootElement.GetProperty("medals").ValueKind
                .Should().Be(JsonValueKind.Array);
            tabs.Should().Contain("overview");
            detail.RootElement.GetProperty("labels").GetProperty("fixtures")
                .GetString().Should().Be("Fixtures");
            var fixtures = detail.RootElement.GetProperty("fixtures");
            fixtures.ValueKind
                .Should().Be(JsonValueKind.Array);
            var results = detail.RootElement.GetProperty("results");
            results.ValueKind
                .Should().Be(JsonValueKind.Array);
            var standings = detail.RootElement.GetProperty("standings");
            standings.ValueKind
                .Should().Be(JsonValueKind.Array);
            var participants = detail.RootElement.GetProperty("participants");
            participants.ValueKind
                .Should().Be(JsonValueKind.Array);
            detail.RootElement.GetProperty("statistics")
                .GetProperty("contestCount").GetInt32()
                .Should().Be(fixtures.GetArrayLength());
            tabs.Contains("fixtures").Should().Be(fixtures.GetArrayLength() > 0);
            foreach (var fixture in fixtures.EnumerateArray())
                fixture.GetProperty("renderer").GetString()
                    .Should().NotBeNullOrWhiteSpace();
            tabs.Contains("results").Should().Be(results.GetArrayLength() > 0);
            tabs.Contains("standings").Should().Be(standings.GetArrayLength() > 0);
            tabs.Contains("participants").Should().Be(participants.GetArrayLength() > 0);
        }
        (await client.GetAsync(
            $"/api/public/tenants/nba/competitions/{footballId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var golfId = Guid.Parse(
            "00000000-0000-0000-0000-000000000302");
        using var golf = JsonDocument.Parse(await (await client.GetAsync(
            $"/api/public/tenants/pga-tour/competitions/{golfId}"))
            .Content.ReadAsStringAsync());
        golf.RootElement.GetProperty("labels").GetProperty("fixtures")
            .GetString().Should().Be("Tee times");
        golf.RootElement.GetProperty("labels").GetProperty("participants")
            .GetString().Should().Be("Golfers");
        golf.RootElement.GetProperty("availableTabs")
            .EnumerateArray().Select(item => item.GetString())
            .Should().Contain("leaderboard");
    }

    [Fact]
    public async Task CompetitionPresentationSettings_AreValidatedPersistedAndPubliclyApplied()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@premier.com", "TenantAdmin123!",
            $"presentation-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var competitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");
        var route = $"/api/competitions/{competitionId}/presentation";

        try
        {
            var update = await client.PutAsJsonAsync(route, new
            {
                primaryContestRenderer = "cricket-scorecard",
                visibleTabs = new[] { "overview", "fixtures" },
                defaultTab = "fixtures",
                scoreVariant = "compact",
                standingsVariant = "full",
                leaderboardVariant = (string?)null,
                bracketEnabled = false,
                seriesEnabled = false,
                medalTableEnabled = false
            });
            update.StatusCode.Should().Be(HttpStatusCode.OK,
                await update.Content.ReadAsStringAsync());

            var stored = await client.GetFromJsonAsync<JsonElement>(route);
            stored.GetProperty("defaultTab").GetString().Should().Be("fixtures");
            stored.GetProperty("visibleTabs").EnumerateArray()
                .Select(item => item.GetString()).Should().BeEquivalentTo("overview", "fixtures");

            client.DefaultRequestHeaders.Authorization = null;
            var detail = await client.GetFromJsonAsync<JsonElement>(
                $"/api/public/tenants/premier-league/competitions/{competitionId}");
            detail.GetProperty("presentation").GetProperty("defaultTab")
                .GetString().Should().Be("fixtures");
            detail.GetProperty("availableTabs").EnumerateArray()
                .Select(item => item.GetString()).Should().BeEquivalentTo("overview", "fixtures");
            detail.GetProperty("fixtures").EnumerateArray()
                .Should().OnlyContain(item => item.GetProperty("renderer").GetString()
                    == "cricket-scorecard");
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", admin.AccessToken);
            await client.PutAsJsonAsync(route, new
            {
                primaryContestRenderer = (string?)null,
                visibleTabs = Array.Empty<string>(),
                defaultTab = (string?)null,
                scoreVariant = (string?)null,
                standingsVariant = (string?)null,
                leaderboardVariant = (string?)null,
                bracketEnabled = (bool?)null,
                seriesEnabled = (bool?)null,
                medalTableEnabled = (bool?)null
            });
        }
    }

    [Fact]
    public async Task PublicContest_ChoosesRenderer_AndAnonymousHubRestoresSnapshot()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var contestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var participants = await database.Participants
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id)
                .Take(2)
                .ToArrayAsync();
            participants.Should().HaveCount(2);
            var contest = new Contest(contestId, tenantId, sportId, "TeamVsTeam");
            contest.SetName("Public recovery contest");
            foreach (var participantId in participants)
                contest.AddParticipant(participantId, "Team");
            contest.Schedule("Public Ground", DateTime.UtcNow.AddHours(1), 90);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/public/tenants/premier-league/contests/{contestId}");
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using (var detail = JsonDocument.Parse(
                   await response.Content.ReadAsStringAsync()))
        {
            detail.RootElement.GetProperty("renderer").GetString()
                .Should().Be("head-to-head");
            detail.RootElement.GetProperty("configuration").ValueKind
                .Should().Be(JsonValueKind.Object);
            detail.RootElement.GetProperty("liveScores").ValueKind
                .Should().Be(JsonValueKind.Array);
        }
        (await client.GetAsync(
            $"/api/public/tenants/nba/contests/{contestId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        HubConnection BuildPublicConnection() => new HubConnectionBuilder()
            .WithUrl(
                new Uri(client.BaseAddress!, "/hubs/contest"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory =
                        _ => _factory.Server.CreateHandler();
                })
            .Build();

        await using var initialConnection = BuildPublicConnection();
        var initialSnapshot = new TaskCompletionSource<ContestSnapshotDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        initialConnection.On<ContestSnapshotDto>(
            "ContestSnapshot",
            snapshot => initialSnapshot.TrySetResult(snapshot));
        await initialConnection.StartAsync();
        await initialConnection.InvokeAsync(
            "JoinPublicContestGroup", "premier-league", contestId);
        (await initialSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .ContestId.Should().Be(contestId);
        var denied = () => initialConnection.InvokeAsync(
            "JoinPublicContestGroup", "nba", contestId);
        await denied.Should().ThrowAsync<HubException>()
            .WithMessage("*unavailable*");
        await initialConnection.StopAsync();

        await using var recoveredConnection = BuildPublicConnection();
        var recoveredSnapshot = new TaskCompletionSource<ContestSnapshotDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        recoveredConnection.On<ContestSnapshotDto>(
            "ContestSnapshot",
            snapshot => recoveredSnapshot.TrySetResult(snapshot));
        await recoveredConnection.StartAsync();
        await recoveredConnection.InvokeAsync(
            "JoinPublicContestGroup", "premier-league", contestId);
        (await recoveredSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .ContestId.Should().Be(contestId);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            database.Contests.RemoveRange(
                database.Contests.Where(item => item.Id == contestId));
            await database.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000100", "TeamVsTeam", "head-to-head")]
    [InlineData("00000000-0000-0000-0000-000000000102", "StrokePlayRound", "multi-participant-leaderboard")]
    [InlineData("00000000-0000-0000-0000-000000000103", "Heat", "race-heat")]
    [InlineData("00000000-0000-0000-0000-000000000104", "CricketMatch", "cricket-scorecard")]
    [InlineData("00000000-0000-0000-0000-000000000105", "Bout", "fight-card-bout")]
    public async Task PublicContest_SelectsRendererFromSportAndShape(
        string sportIdValue,
        string shape,
        string expectedRenderer)
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var contestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var participants = await database.Participants
                .Where(item => item.TenantId == tenantId)
                .Select(item => new { item.Id, item.Type })
                .Take(2)
                .ToArrayAsync();
            var contest = new Contest(
                contestId, tenantId, Guid.Parse(sportIdValue), shape);
            contest.SetName($"Public {shape}");
            foreach (var participant in participants)
                contest.AddParticipant(participant.Id, participant.Type.ToString());
            contest.Schedule("Renderer Ground", DateTime.UtcNow.AddHours(1), 90);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        using var detail = JsonDocument.Parse(await (await client.GetAsync(
            $"/api/public/tenants/premier-league/contests/{contestId}"))
            .Content.ReadAsStringAsync());
        detail.RootElement.GetProperty("renderer").GetString()
            .Should().Be(expectedRenderer);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            database.Contests.RemoveRange(
                database.Contests.Where(item => item.Id == contestId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PublicParticipant_SupportsTeamAndAthleteHistoryWithoutSensitiveData()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var athleteId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var athleteUpcomingId = Guid.NewGuid();
        var teamUpcomingId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        var publishedResultId = Guid.NewGuid();
        var privateResultId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var athlete = new Athlete(athleteId, tenantId, "Public Runner", "Runner");
            athlete.UpdatePersonalInfo(
                new DateTime(2000, 1, 2), "Private gender", "Kenya");
            athlete.UpdateContact(
                "private@example.test", "+254700000000", "Private emergency contact");
            athlete.SetMedicalClearance(true);
            athlete.RegisterForSport(sportId);
            var team = new Team(teamId, tenantId, "Public United", "United");
            team.UpdateDetails("Public Ground", "Public Coach", "Public Manager");
            team.AddRoster("First team", null)
                .AddAthlete(athleteId, "9", "Forward");
            var opponent = new Team(opponentId, tenantId, "Public Opponent", "Opponent");

            var athleteUpcoming = new Contest(
                athleteUpcomingId, tenantId, sportId, "Race");
            athleteUpcoming.SetName("Future public race");
            athleteUpcoming.AddParticipant(athleteId, "Athlete");
            athleteUpcoming.Schedule(
                "Future Track", DateTime.UtcNow.AddDays(2), 30);
            var teamUpcoming = new Contest(
                teamUpcomingId, tenantId, sportId, "TeamVsTeam");
            teamUpcoming.SetName("Future public match");
            teamUpcoming.AddParticipant(teamId, "Team");
            teamUpcoming.AddParticipant(opponentId, "Team");
            teamUpcoming.Schedule(
                "Future Ground", DateTime.UtcNow.AddDays(1), 90);
            var completed = new Contest(completedId, tenantId, sportId, "Race");
            completed.SetName("Published final");
            completed.AddParticipant(athleteId, "Athlete");
            completed.Schedule("History Track", DateTime.UtcNow.AddDays(-2), 30);
            completed.Start();
            completed.Complete();

            var publishedResult = new ContestResult(
                publishedResultId, completedId, Guid.NewGuid(), athleteId);
            publishedResult.SetRank(1, "Winner");
            publishedResult.SetScore(10, "10.00", "00:10.00");
            publishedResult.Submit();
            publishedResult.Approve(Guid.NewGuid());
            publishedResult.Publish();
            var privateResult = new ContestResult(
                privateResultId, completedId, Guid.NewGuid(), athleteId);
            privateResult.SetRank(2, "Second");

            database.Participants.AddRange(athlete, team, opponent);
            database.Contests.AddRange(athleteUpcoming, teamUpcoming, completed);
            database.ContestResults.AddRange(publishedResult, privateResult);
            await database.SaveChangesAsync();
        }

        var athleteResponse = await client.GetAsync(
            $"/api/public/tenants/premier-league/participants/{athleteId}");
        athleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        athleteResponse.Headers.CacheControl!.Public.Should().BeTrue();
        var athleteJson = await athleteResponse.Content.ReadAsStringAsync();
        using (var athlete = JsonDocument.Parse(athleteJson))
        {
            athlete.RootElement.GetProperty("profile").GetProperty("kind")
                .GetString().Should().Be("Athlete");
            athlete.RootElement.GetProperty("profile").GetProperty("nationality")
                .GetString().Should().Be("Kenya");
            athlete.RootElement.GetProperty("upcomingContests")
                .EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
                .Should().Contain(athleteUpcomingId);
            athlete.RootElement.GetProperty("results").GetArrayLength().Should().Be(1);
            athlete.RootElement.GetProperty("statistics").GetProperty("wins")
                .GetInt32().Should().Be(1);
        }
        foreach (var sensitiveName in new[]
                 {
                     "dateOfBirth", "gender", "contactEmail", "contactPhone",
                     "emergencyContact", "medicalClearance"
                 })
            athleteJson.Should().NotContain(sensitiveName);

        using var teamPayload = JsonDocument.Parse(await (await client.GetAsync(
            $"/api/public/tenants/premier-league/participants/{teamId}"))
            .Content.ReadAsStringAsync());
        teamPayload.RootElement.GetProperty("profile").GetProperty("kind")
            .GetString().Should().Be("Team");
        teamPayload.RootElement.GetProperty("profile").GetProperty("homeVenue")
            .GetString().Should().Be("Public Ground");
        teamPayload.RootElement.GetProperty("profile").GetProperty("rosters")[0]
            .GetProperty("members")[0].GetProperty("athleteId")
            .GetGuid().Should().Be(athleteId);
        teamPayload.RootElement.GetProperty("upcomingContests")
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .Should().Contain(teamUpcomingId);
        (await client.GetAsync(
            $"/api/public/tenants/nba/participants/{athleteId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            database.ContestResults.RemoveRange(database.ContestResults.Where(
                item => item.Id == publishedResultId || item.Id == privateResultId));
            database.Contests.RemoveRange(database.Contests.Where(item =>
                item.Id == athleteUpcomingId || item.Id == teamUpcomingId
                || item.Id == completedId));
            database.Participants.RemoveRange(database.Participants.Where(item =>
                item.Id == athleteId || item.Id == teamId || item.Id == opponentId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PublicSearch_IsTenantScopedFilteredAndPaginatedAcrossEntities()
    {
        using var client = _factory.CreateClient();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var competitionId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var draftContestId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var team = new Team(teamId, tenantId, "Searchable Team", "Search Team");
            var athlete = new Athlete(
                athleteId, tenantId, "Searchable Athlete", "Search Athlete");
            var competition = new Competition(
                competitionId, tenantId, "Searchable Cup", sportId);
            competition.Publish();
            var edition = competition.AddEdition(
                "Search Edition", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10));
            var stage = edition.AddStage("Search Stage", StageType.League, 1);
            var contest = new Contest(contestId, tenantId, sportId, "TeamVsTeam");
            contest.SetName("Searchable Final");
            contest.AssignToStage(stage.Id, 1);
            contest.AddParticipant(teamId, "Team");
            contest.AddParticipant(athleteId, "Athlete");
            contest.Schedule("Searchable Arena", DateTime.UtcNow.AddDays(3), 90);
            var draftContest = new Contest(
                draftContestId, tenantId, sportId, "TeamVsTeam");
            draftContest.SetName("Private Draft Contest");
            draftContest.AddParticipant(teamId, "Team");
            draftContest.Schedule("Private Draft Venue", DateTime.UtcNow.AddDays(4), 90);
            draftContest.Cancel();
            database.Participants.AddRange(team, athlete);
            database.Competitions.Add(competition);
            database.Contests.AddRange(contest, draftContest);
            await database.SaveChangesAsync();
        }

        var firstResponse = await client.GetAsync(
            "/api/public/tenants/premier-league/search?q=Searchable&page=1&pageSize=2");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstResponse.Headers.CacheControl!.Public.Should().BeTrue();
        using var first = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync());
        first.RootElement.GetProperty("total").GetInt32().Should().Be(5);
        first.RootElement.GetProperty("totalPages").GetInt32().Should().Be(3);
        first.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
        using var last = JsonDocument.Parse(await (await client.GetAsync(
            "/api/public/tenants/premier-league/search?q=Searchable&page=3&pageSize=2"))
            .Content.ReadAsStringAsync());
        last.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);

        var from = Uri.EscapeDataString(DateTime.UtcNow.Date.ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(5).ToString("O"));
        using var filtered = JsonDocument.Parse(await (await client.GetAsync(
            $"/api/public/tenants/premier-league/search?type=contest"
            + $"&sportId={sportId}&status=Scheduled&competitionId={competitionId}"
            + $"&dateFrom={from}&dateTo={to}"))
            .Content.ReadAsStringAsync());
        filtered.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        filtered.RootElement.GetProperty("items")[0].GetProperty("id")
            .GetString().Should().Be(contestId.ToString());
        using var teams = JsonDocument.Parse(await (await client.GetAsync(
            $"/api/public/tenants/premier-league/search?type=team"
            + $"&sportId={sportId}&competitionId={competitionId}&q=Searchable"))
            .Content.ReadAsStringAsync());
        teams.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .Should().Contain(teamId.ToString());
        using var sports = JsonDocument.Parse(await (await client.GetAsync(
            "/api/public/tenants/premier-league/search?type=sport&q=Football"))
            .Content.ReadAsStringAsync());
        sports.RootElement.GetProperty("items").EnumerateArray()
            .Should().ContainSingle(item =>
                item.GetProperty("type").GetString() == "Sport"
                && item.GetProperty("href").GetString() == "/sport/football");
        using var privateVenue = JsonDocument.Parse(await (await client.GetAsync(
            "/api/public/tenants/premier-league/search?type=venue&q=Private%20Draft%20Venue"))
            .Content.ReadAsStringAsync());
        privateVenue.RootElement.GetProperty("total").GetInt32().Should().Be(0);
        using var wrongTenant = JsonDocument.Parse(await (await client.GetAsync(
            "/api/public/tenants/nba/search?q=Searchable"))
            .Content.ReadAsStringAsync());
        wrongTenant.RootElement.GetProperty("total").GetInt32().Should().Be(0);
        (await client.GetAsync(
            "/api/public/tenants/premier-league/search?pageSize=51"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            database.Contests.RemoveRange(database.Contests.Where(item =>
                item.Id == contestId || item.Id == draftContestId));
            database.Competitions.RemoveRange(database.Competitions.Where(
                item => item.Id == competitionId));
            database.Participants.RemoveRange(database.Participants.Where(item =>
                item.Id == teamId || item.Id == athleteId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ProtectedRoutes_RejectAnonymousRequests()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/competitions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SeededAdministrator_CanLoginAndReadProtectedResources()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var tenants = await client.GetAsync("/api/tenants");
        var competitions = await client.GetAsync(
            $"/api/competitions?tenantId={token.TenantId}");

        tenants.StatusCode.Should().Be(HttpStatusCode.OK);
        competitions.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Notifications_AreUserScopedAndDeviceRegistrationTracksLifecycle()
    {
        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            $"notification-owner-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);

        Guid ownerId;
        Guid otherId;
        var ownerNotificationId = Guid.NewGuid();
        var otherNotificationId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            ownerId = await database.Users
                .Where(item => item.Email == "admin@premier.com")
                .Select(item => item.Id)
                .SingleAsync();
            otherId = await database.Users
                .Where(item => item.Email == "admin@sportsplatform.com")
                .Select(item => item.Id)
                .SingleAsync();
            var owned = new Notification(
                ownerNotificationId,
                ownerId,
                NotificationType.Contest,
                NotificationChannel.InApp,
                "Fixture moved",
                "The fixture has a new start time.");
            owned.SetMetadata(JsonSerializer.Serialize(new
            {
                contestId = Guid.Parse("00000000-0000-0000-0000-000000000400")
            }));
            database.Notifications.Add(owned);
            database.Notifications.Add(new Notification(
                otherNotificationId,
                otherId,
                NotificationType.System,
                NotificationChannel.InApp,
                "Other user's notice",
                "Must not be returned."));
            await database.SaveChangesAsync();
        }

        var inbox = await client.GetFromJsonAsync<JsonElement>(
            "/api/notifications?page=1&pageSize=100");
        var items = inbox.GetProperty("data").EnumerateArray().ToArray();
        items.Should().Contain(item =>
            item.GetProperty("id").GetGuid() == ownerNotificationId
            && item.GetProperty("payload").GetString()!.StartsWith("contest:"));
        items.Should().NotContain(item =>
            item.GetProperty("id").GetGuid() == otherNotificationId);
        (await client.GetFromJsonAsync<JsonElement>(
            "/api/notifications/unread-count"))
            .GetProperty("count").GetInt32().Should().BeGreaterThan(0);

        (await client.PutAsync(
            $"/api/notifications/{ownerNotificationId}/read", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsync(
            $"/api/notifications/{otherNotificationId}/read", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var token = $"test-fcm-token-{Guid.NewGuid():N}";
        var deviceId = $"test-device-{Guid.NewGuid():N}";
        var registered = await client.PutAsJsonAsync(
            "/api/notifications/devices/current",
            new { token, platform = "android", deviceId });
        registered.StatusCode.Should().Be(HttpStatusCode.OK,
            await registered.Content.ReadAsStringAsync());

        var platformAdmin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            $"notification-other-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", platformAdmin.AccessToken);
        (await client.PutAsJsonAsync(
            "/api/notifications/devices/current",
            new { token, platform = "ios", deviceId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var registration = await database.PushDeviceRegistrations
                .SingleAsync(item => item.Token == token);
            registration.UserId.Should().Be(otherId);
            registration.Platform.Should().Be("ios");
        }

        var disabled = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            "/api/notifications/devices/current")
        {
            Content = JsonContent.Create(new { token, deviceId })
        });
        disabled.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            (await database.PushDeviceRegistrations
                .SingleAsync(item => item.Token == token))
                .IsEnabled.Should().BeFalse();
            database.PushDeviceRegistrations.RemoveRange(
                database.PushDeviceRegistrations.Where(item => item.Token == token));
            database.Notifications.RemoveRange(
                database.Notifications.Where(item =>
                    item.Id == ownerNotificationId || item.Id == otherNotificationId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task NotificationCampaigns_AreTenantScopedIdempotentAuditedAndDeepLinked()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            $"notification-campaign-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        Guid competitionId;
        Guid contestId;
        int recipientCount;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var stageIds = await database.Competitions
                .Where(item => item.TenantId == admin.TenantId)
                .SelectMany(item => item.Editions)
                .SelectMany(item => item.Stages)
                .Select(item => item.Id)
                .ToArrayAsync();
            var contest = await database.Contests.AsNoTracking()
                .FirstAsync(item => item.StageId.HasValue
                    && stageIds.Contains(item.StageId.Value));
            contestId = contest.Id;
            competitionId = await database.Competitions
                .Where(item => item.TenantId == admin.TenantId
                    && item.Editions.SelectMany(edition => edition.Stages)
                        .Any(stage => stage.Id == contest.StageId!.Value))
                .Select(item => item.Id)
                .SingleAsync();
            recipientCount = await database.Users.CountAsync(item =>
                item.TenantId == admin.TenantId && item.IsActive && !item.IsSuspended);
        }

        var reminderKey = $"fixture-{Guid.NewGuid():N}";
        var reminderRequest = new
        {
            tenantId = admin.TenantId,
            type = "FixtureReminder",
            title = "Fixture starts soon",
            body = "Your followed fixture begins shortly.",
            reason = "Scheduled match-day reminder",
            idempotencyKey = reminderKey,
            competitionId,
            contestId
        };
        var created = await client.PostAsJsonAsync(
            "/api/notifications/campaigns", reminderRequest);
        created.StatusCode.Should().Be(HttpStatusCode.Created,
            await created.Content.ReadAsStringAsync());
        var createdPayload = await created.Content.ReadFromJsonAsync<JsonElement>();
        var reminderId = createdPayload.GetProperty("id").GetGuid();
        createdPayload.GetProperty("recipientCount").GetInt32().Should().Be(recipientCount);

        var replay = await client.PostAsJsonAsync(
            "/api/notifications/campaigns", reminderRequest);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()
            .Should().Be(reminderId);

        var changedReplay = await client.PostAsJsonAsync(
            "/api/notifications/campaigns",
            new
            {
                reminderRequest.tenantId,
                reminderRequest.type,
                title = "Changed content",
                reminderRequest.body,
                reminderRequest.reason,
                reminderRequest.idempotencyKey,
                reminderRequest.competitionId,
                reminderRequest.contestId
            });
        changedReplay.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var announcement = await client.PostAsJsonAsync(
            "/api/notifications/campaigns",
            new
            {
                tenantId = admin.TenantId,
                type = "Announcement",
                title = "Competition update",
                body = "The competition information has been updated.",
                reason = "Publish organizer update",
                idempotencyKey = $"announcement-{Guid.NewGuid():N}",
                competitionId
            });
        announcement.StatusCode.Should().Be(HttpStatusCode.Created,
            await announcement.Content.ReadAsStringAsync());
        var announcementId = (await announcement.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var history = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/notifications/campaigns?tenantId={admin.TenantId}");
        history.Should().Contain(item => item.GetProperty("id").GetGuid() == reminderId
            && item.GetProperty("type").GetString() == "FixtureReminder");
        history.Should().Contain(item => item.GetProperty("id").GetGuid() == announcementId
            && item.GetProperty("type").GetString() == "Announcement");

        var inbox = await client.GetFromJsonAsync<JsonElement>(
            "/api/notifications?page=1&pageSize=100");
        var inboxItems = inbox.GetProperty("data").EnumerateArray().ToArray();
        inboxItems.Should().Contain(item =>
            item.GetProperty("data").GetProperty("campaignId").GetGuid() == reminderId
            && item.GetProperty("data").GetProperty("type").GetString() == "fixture_reminder"
            && item.GetProperty("payload").GetString() == $"contest:{contestId}");
        inboxItems.Should().Contain(item =>
            item.GetProperty("data").GetProperty("campaignId").GetGuid() == announcementId
            && item.GetProperty("data").GetProperty("type").GetString() == "announcement"
            && item.GetProperty("payload").GetString() == $"competition:{competitionId}");

        var foreignTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        (await client.GetAsync($"/api/notifications/campaigns?tenantId={foreignTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/notifications/campaigns", new
        {
            tenantId = foreignTenantId,
            type = "Announcement",
            title = "Forbidden",
            body = "Must not dispatch.",
            reason = "Isolation check",
            idempotencyKey = Guid.NewGuid().ToString("N")
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            (await database.NotificationCampaigns.CountAsync(item =>
                item.TenantId == admin.TenantId && item.IdempotencyKey == reminderKey))
                .Should().Be(1);
            (await database.Notifications.CountAsync(item => item.Metadata != null
                && item.Metadata.Contains(reminderId.ToString())))
                .Should().Be(recipientCount);
            (await database.AuditEntries.AnyAsync(item =>
                item.Action == "NotificationCampaignDispatched"
                && item.EntityId == reminderId.ToString()
                && item.TenantId == admin.TenantId)).Should().BeTrue();

            var notifications = await database.Notifications.Where(item => item.Metadata != null
                && (item.Metadata.Contains(reminderId.ToString())
                    || item.Metadata.Contains(announcementId.ToString())))
                .ToArrayAsync();
            var campaigns = await database.NotificationCampaigns.Where(item =>
                item.Id == reminderId || item.Id == announcementId).ToArrayAsync();
            database.Notifications.RemoveRange(notifications);
            database.NotificationCampaigns.RemoveRange(campaigns);
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SeededScorer_CannotQueryAnotherTenantsCompetitions()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var response = await client.GetAsync(
            $"/api/competitions?tenantId={nbaTenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public Task ProtectedTenantMatrix_IsEnforcedByInMemoryApi()
    {
        return TenantIsolationMatrix.AssertAsync(_factory);
    }

    [Fact]
    public async Task ContestHub_DeniesCrossTenantGroupMembership()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "signalr-tenant-isolation");
        Guid premierContestId;
        Guid nbaContestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            premierContestId = await database.Contests
                .Where(contest => contest.TenantId == token.TenantId)
                .Select(contest => contest.Id)
                .FirstAsync();
            nbaContestId = await database.Contests
                .Where(contest =>
                    contest.TenantId ==
                    Guid.Parse("00000000-0000-0000-0000-000000000003"))
                .Select(contest => contest.Id)
                .FirstAsync();
        }

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(client.BaseAddress!, "/hubs/contest"),
                options =>
                {
                    options.AccessTokenProvider =
                        () => Task.FromResult<string?>(token.AccessToken);
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory =
                        _ => _factory.Server.CreateHandler();
                })
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync("JoinContestGroup", premierContestId);
        var denied = () =>
            connection.InvokeAsync("JoinContestGroup", nbaContestId);
        await denied.Should().ThrowAsync<HubException>()
            .WithMessage("*unavailable for this tenant*");
    }

    [Fact]
    public async Task ContestHub_DeliversGroupUpdates_AndSnapshotAfterReconnect()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "signalr-delivery-and-reconnect");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        Guid participantId;
        Guid segmentId;
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            participantId = await database.Participants
                .Where(item => item.TenantId == token.TenantId)
                .Select(item => item.Id)
                .FirstAsync();
            userId = await database.Users
                .Where(item => item.Email == "scorer@premier.com")
                .Select(item => item.Id)
                .SingleAsync();
            var sportId = Guid.Parse("00000000-0000-0000-0000-000000000101");
            var competition = new Competition(
                competitionId, token.TenantId, "SignalR delivery verification", sportId);
            var edition = competition.AddEdition(
                "2026", DateTime.UtcNow, DateTime.UtcNow.AddDays(2));
            var stage = edition.AddStage("Live", StageType.League, 1);
            var contest = new Contest(contestId, token.TenantId, sportId, "TeamVsTeam");
            contest.SetName("SignalR snapshot contest");
            contest.AssignToStage(stage.Id, 1);
            contest.AddParticipant(participantId, "Team");
            segmentId = contest.AddSegment("First Half", "Half", 1).Id;
            contest.Schedule("Realtime Ground", DateTime.UtcNow.AddHours(1), 90);
            database.Competitions.Add(competition);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        HubConnection BuildConnection() => new HubConnectionBuilder()
            .WithUrl(
                new Uri(client.BaseAddress!, "/hubs/contest"),
                options =>
                {
                    options.AccessTokenProvider =
                        () => Task.FromResult<string?>(token.AccessToken);
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory =
                        _ => _factory.Server.CreateHandler();
                })
            .Build();

        await using var contestConnection = BuildConnection();
        await using var participantConnection = BuildConnection();
        await using var competitionConnection = BuildConnection();
        var initialSnapshot = new TaskCompletionSource<ContestSnapshotDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var contestUpdate = new TaskCompletionSource<LiveScoreUpdateDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var contestPatch = new TaskCompletionSource<ContestLiveUpdateDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var participantUpdate = new TaskCompletionSource<LiveScoreUpdateDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var competitionUpdate = new TaskCompletionSource<LiveScoreUpdateDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        contestConnection.On<ContestSnapshotDto>(
            "ContestSnapshot", snapshot => initialSnapshot.TrySetResult(snapshot));
        contestConnection.On<LiveScoreUpdateDto>(
            "LiveScoreUpdated", update => contestUpdate.TrySetResult(update));
        contestConnection.On<ContestLiveUpdateDto>(
            "ContestLiveUpdated", update => contestPatch.TrySetResult(update));
        participantConnection.On<LiveScoreUpdateDto>(
            "LiveScoreUpdated", update => participantUpdate.TrySetResult(update));
        competitionConnection.On<LiveScoreUpdateDto>(
            "LiveScoreUpdated", update => competitionUpdate.TrySetResult(update));
        await Task.WhenAll(
            contestConnection.StartAsync(),
            participantConnection.StartAsync(),
            competitionConnection.StartAsync());
        await contestConnection.InvokeAsync("JoinTenantGroup", token.TenantId);
        await contestConnection.InvokeAsync("JoinUserGroup", userId);
        var deniedUser = () => contestConnection.InvokeAsync("JoinUserGroup", Guid.NewGuid());
        await deniedUser.Should().ThrowAsync<HubException>();
        await contestConnection.InvokeAsync("JoinContestGroup", contestId);
        await participantConnection.InvokeAsync("JoinParticipantGroup", participantId);
        await competitionConnection.InvokeAsync("JoinCompetitionGroup", competitionId);
        (await initialSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .ContestId.Should().Be(contestId);

        var eventId = Guid.NewGuid();
        var submitted = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId,
                participantId,
                metricName = "score",
                value = 1,
                segmentId,
                deviceTimestamp = DateTime.UtcNow,
                deviceId = "signalr-delivery-device",
                sequenceNumber = 1,
                idempotencyKey = eventId.ToString("N")
            });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK,
            await submitted.Content.ReadAsStringAsync());
        var delivered = await Task.WhenAll(
            contestUpdate.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            participantUpdate.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            competitionUpdate.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        delivered.Should().OnlyContain(item =>
            item.ContestId == contestId
            && item.ParticipantId == participantId
            && item.MetricName == "score"
            && item.Value == 1);
        var patch = await contestPatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        patch.ContestId.Should().Be(contestId);
        patch.Version.Should().BePositive();
        patch.Participants.Should().ContainSingle(item =>
            item.ParticipantId == participantId && item.Score == "1");

        await contestConnection.StopAsync();
        await using var reconnected = BuildConnection();
        var recoveredSnapshot = new TaskCompletionSource<ContestSnapshotDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        reconnected.On<ContestSnapshotDto>(
            "ContestSnapshot", snapshot => recoveredSnapshot.TrySetResult(snapshot));
        await reconnected.StartAsync();
        await reconnected.InvokeAsync("JoinContestGroup", contestId);
        var recovered = await recoveredSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        recovered.ContestId.Should().Be(contestId);
        recovered.RecentScores.Should().ContainSingle(item =>
            item.ContestId == contestId
            && item.ParticipantId == participantId
            && item.MetricName == "score"
            && item.Value == 1);
        recovered.ParticipantScores.Should().ContainSingle(item =>
            item.ParticipantId == participantId && item.Score == "1");

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.LiveScores.RemoveRange(
                database.LiveScores.Where(item => item.ContestId == contestId));
            database.Contests.RemoveRange(
                database.Contests.Where(item => item.Id == contestId));
            database.Competitions.RemoveRange(
                database.Competitions.Where(item => item.Id == competitionId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SubscriptionLifecycleJob_RequiresAndHonorsTenantScope()
    {
        var premierTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000003");
        var premierSubscription = new Subscription(
            Guid.NewGuid(),
            premierTenantId,
            "Tenant job matrix",
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow.AddDays(3));
        var nbaSubscription = new Subscription(
            Guid.NewGuid(),
            nbaTenantId,
            "Tenant job matrix",
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow.AddDays(3));
        premierSubscription.Activate();
        nbaSubscription.Activate();

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider
            .GetRequiredService<SportsPlatformDbContext>();
        database.Subscriptions.AddRange(
            premierSubscription,
            nbaSubscription);
        await database.SaveChangesAsync();

        var job = scope.ServiceProvider
            .GetRequiredService<SubscriptionLifecycleJob>();
        var missingTenant = () => job.ExecuteAsync(Guid.Empty);
        await missingTenant.Should().ThrowAsync<ArgumentException>();
        await job.ExecuteAsync(premierTenantId);

        database.ChangeTracker.Clear();
        (await database.Subscriptions.FindAsync(premierSubscription.Id))!
            .Status.Should().Be(SubscriptionStatus.Expiring);
        (await database.Subscriptions.FindAsync(nbaSubscription.Id))!
            .Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task ExpiredLease_BecomesReadOnlyThenRenewalRestoresWrites()
    {
        var tenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");
        var footballSportId =
            Guid.Parse("00000000-0000-0000-0000-000000000100");
        Guid subscriptionId;
        Guid contestId;
        Guid participantId;
        Guid segmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            participantId = await database.Participants
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id)
                .FirstAsync();
            var subscription = new Subscription(
                Guid.NewGuid(),
                tenantId,
                "Lease expiry acceptance",
                DateTime.UtcNow.AddMonths(-1),
                DateTime.UtcNow.AddDays(-8));
            subscription.Activate();
            subscriptionId = subscription.Id;
            var contest = new SportsPlatform.Modules.Contests.Entities.Contest(
                Guid.NewGuid(),
                tenantId,
                footballSportId,
                "LeaseAcceptance");
            contest.SetName("Lease expiry scoring");
            contest.AddParticipant(participantId, "Team");
            segmentId = contest.AddSegment("First Half", "Half", 1).Id;
            contest.AddSegment("Second Half", "Half", 2);
            contest.Schedule("Lease Test Ground", DateTime.UtcNow.AddHours(1));
            contestId = contest.Id;
            database.Subscriptions.Add(subscription);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();

            var job = scope.ServiceProvider
                .GetRequiredService<SubscriptionLifecycleJob>();
            await job.ExecuteAsync(tenantId);
            database.ChangeTracker.Clear();
            (await database.Subscriptions.FindAsync(subscriptionId))!
                .Status.Should().Be(SubscriptionStatus.GracePeriod);
        }

        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-lease-expiry-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        async Task<HttpResponseMessage> ScoreAsync() =>
            await client.PostAsJsonAsync(
                $"/api/livescoring/contests/{contestId}/events",
                new
                {
                    eventId = Guid.NewGuid(),
                    participantId,
                    metricName = "score",
                    value = 1,
                    scoredBy = "lease-acceptance",
                    segmentId
                });

        var graceWrite = await ScoreAsync();
        graceWrite.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await graceWrite.Content.ReadAsStringAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider
                .GetRequiredService<SubscriptionLifecycleJob>();
            await job.ExecuteAsync(tenantId);
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            database.ChangeTracker.Clear();
            (await database.Subscriptions.FindAsync(subscriptionId))!
                .Status.Should().Be(SubscriptionStatus.ReadOnly);
        }

        var publicView = await client.GetAsync(
            "/api/public/tenants/premier-league/home");
        publicView.StatusCode.Should().Be(HttpStatusCode.OK);
        var blockedWrite = await ScoreAsync();
        blockedWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var blockedPayload = await blockedWrite.Content
            .ReadFromJsonAsync<JsonElement>();
        blockedPayload.GetProperty("error").GetString()
            .Should().Be("SubscriptionInReadOnlyMode");
        blockedPayload.GetProperty("status").GetString()
            .Should().Be("ReadOnly");

        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-lease-expiry-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var renewal = await client.PostAsJsonAsync(
            $"/api/subscriptions/{subscriptionId}/renew",
            new { endDateUtc = DateTime.UtcNow.AddYears(1) });
        renewal.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await renewal.Content.ReadAsStringAsync());
        var renewed = await renewal.Content.ReadFromJsonAsync<JsonElement>();
        renewed.GetProperty("status").GetInt32()
            .Should().Be((int)SubscriptionStatus.Active);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);
        var resumedWrite = await ScoreAsync();
        resumedWrite.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await resumedWrite.Content.ReadAsStringAsync());

        using (var cleanupScope = _factory.Services.CreateScope())
        {
            var database = cleanupScope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var testContest = await database.Contests
                .Include(item => item.Participants)
                .Include(item => item.Segments)
                .Include(item => item.Events)
                .SingleAsync(item => item.Id == contestId);
            var testSubscription = await database.Subscriptions
                .SingleAsync(item => item.Id == subscriptionId);
            database.Contests.Remove(testContest);
            database.Subscriptions.Remove(testSubscription);
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SeededScorer_CanSubmitAndReadOwnTenantLiveScore()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-live-score");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={token.TenantId}");
        contestsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contest = contests.RootElement.EnumerateArray().First();
        var contestId = contest.GetProperty("id").GetGuid();
        var participantId = contest.GetProperty("participants")[0]
            .GetProperty("participantId")
            .GetGuid();
        var segmentId = contest.GetProperty("segments")[0]
            .GetProperty("id")
            .GetGuid();

        var submit = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId,
                metricName = "score",
                value = 1,
                scoredBy = "integration-test",
                segmentId,
                clockSeconds = 12.5m
            });
        var scores = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/scores");

        submit.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await submit.Content.ReadAsStringAsync());
        scores.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scorePayload = JsonDocument.Parse(
            await scores.Content.ReadAsStringAsync());
        scorePayload.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SeededScorer_GetsContestScopedSportScoringSchema()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-scoring-schema");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={token.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var footballContest = contests.RootElement.EnumerateArray()
            .First(contest => contest.GetProperty("sportId").GetGuid() ==
                Guid.Parse("00000000-0000-0000-0000-000000000100"));

        var response = await client.GetAsync(
            $"/api/livescoring/contests/{footballContest.GetProperty("id").GetGuid()}/schema");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        using var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("sportCode").GetString()
            .Should().Be("football");
        payload.RootElement.GetProperty("actions").EnumerateArray()
            .Should().Contain(action =>
                action.GetProperty("code").GetString() == "goal");
        payload.RootElement.GetProperty("segments").GetArrayLength()
            .Should().BeGreaterThan(0);

        var invalidEvent = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{footballContest.GetProperty("id").GetGuid()}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId = footballContest.GetProperty("participants")[0]
                    .GetProperty("participantId").GetGuid(),
                metricName = "invented_metric",
                value = 99,
                scoredBy = "schema-validation-test"
            });
        invalidEvent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LiveScoreEventId_IsIdempotentWhenMobileReplayRetries()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-mobile-replay");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={token.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contest = contests.RootElement.EnumerateArray().First();
        var contestId = contest.GetProperty("id").GetGuid();
        var participantId = contest.GetProperty("participants")[0]
            .GetProperty("participantId")
            .GetGuid();
        var segmentId = contest.GetProperty("segments")[0]
            .GetProperty("id")
            .GetGuid();
        var eventId = Guid.NewGuid();
        var request = new
        {
            eventId,
            participantId,
            metricName = "score",
            value = 1,
            scoredBy = "mobile-offline-replay",
            segmentId,
            clockSeconds = 34m
        };

        var first = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            request);
        var replay = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            request);
        var scores = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/livescoring/contests/{contestId}/scores");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        scores.Should().ContainSingle(item =>
            item.GetProperty("eventId").GetGuid() == eventId);
    }

    [Fact]
    public async Task Corrections_ArePermissionAwareContestBoundAndAudited()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-correction-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={scorer.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contest = contests.RootElement.EnumerateArray().First();
        var contestId = contest.GetProperty("id").GetGuid();
        var participantId = contest.GetProperty("participants")[0]
            .GetProperty("participantId").GetGuid();
        var segmentId = contest.GetProperty("segments")[0]
            .GetProperty("id").GetGuid();
        var eventId = Guid.NewGuid();
        var submit = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId,
                participantId,
                metricName = "score",
                value = 1,
                scoredBy = "correction-test",
                segmentId,
                clockSeconds = 42m
            });
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        using var submitted = JsonDocument.Parse(
            await submit.Content.ReadAsStringAsync());
        var scoreId = submitted.RootElement.GetProperty("eventId").GetGuid();

        var scorerCorrection = await client.PutAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}",
            new { newValue = 1, updatedBy = "scorer", reason = "check" });
        scorerCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-correction-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var correction = await client.PutAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}",
            new { newValue = 1, updatedBy = "admin", reason = "verified" });
        correction.StatusCode.Should().Be(
            HttpStatusCode.Accepted,
            await correction.Content.ReadAsStringAsync());
        using var correctionJson = JsonDocument.Parse(
            await correction.Content.ReadAsStringAsync());
        var correctionId = correctionJson.RootElement.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/corrections/{correctionId}/approve",
            new { reason = "Correction approved" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var reverse = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}/reverse",
            new { reversedBy = "admin", reason = "official reversal" });
        reverse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var reverseJson = JsonDocument.Parse(await reverse.Content.ReadAsStringAsync());
        var reverseId = reverseJson.RootElement.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/corrections/{reverseId}/approve",
            new { reason = "Reversal approved" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var scoresResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/scores");
        using var scores = JsonDocument.Parse(
            await scoresResponse.Content.ReadAsStringAsync());
        var score = scores.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == scoreId);
        score.GetProperty("value").GetDecimal().Should().Be(0);
        score.GetProperty("segmentId").GetGuid().Should().Be(segmentId);
        score.GetProperty("clockSeconds").GetDecimal().Should().Be(42m);

        var historyResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/scores/history");
        using var history = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());
        history.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CorrectionApproval_PreservesPendingState_RecalculatesAndBroadcasts()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client, "scorer@premier.com", "Scorer123!", "correction-approval-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);
        var contestId = Guid.NewGuid();
        Guid participantId;
        Guid segmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            participantId = await database.Participants
                .Where(item => item.TenantId == scorer.TenantId)
                .Select(item => item.Id)
                .FirstAsync();
            var contest = new Contest(
                contestId,
                scorer.TenantId,
                Guid.Parse("00000000-0000-0000-0000-000000000102"),
                "MultiParticipant");
            contest.SetName("Correction approval verification");
            contest.AddParticipant(participantId, "Athlete");
            segmentId = contest.AddSegment("Round 1", "Round", 1).Id;
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }
        var eventId = Guid.NewGuid();
        var submitted = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId,
                participantId,
                metricName = "strokes",
                value = 1,
                segmentId,
                deviceId = "correction-approval-device",
                idempotencyKey = eventId.ToString("N")
            });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK,
            await submitted.Content.ReadAsStringAsync());
        using var submittedJson = JsonDocument.Parse(
            await submitted.Content.ReadAsStringAsync());
        var scoreId = submittedJson.RootElement.GetProperty("eventId").GetGuid();

        var admin = await LoginAsync(
            client, "admin@sportsplatform.com", "Admin123!", "correction-approval-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/contest"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(admin.AccessToken);
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        var correctionBroadcast = new TaskCompletionSource<LiveScoreUpdateDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<LiveScoreUpdateDto>("LiveScoreUpdated", update =>
        {
            if (update.UpdateType == "CorrectApproved")
                correctionBroadcast.TrySetResult(update);
        });
        await connection.StartAsync();
        await connection.InvokeAsync("JoinContestGroup", contestId);

        var requested = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}/corrections",
            new { kind = "Correct", reason = "Scorekeeper transcription", value = 3 });
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted,
            await requested.Content.ReadAsStringAsync());
        using var requestedJson = JsonDocument.Parse(await requested.Content.ReadAsStringAsync());
        var correctionId = requestedJson.RootElement.GetProperty("id").GetGuid();

        var pendingScores = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/livescoring/contests/{contestId}/scores");
        pendingScores!.Single(item => item.GetProperty("id").GetGuid() == scoreId)
            .GetProperty("value").GetDecimal().Should().Be(1);

        var approved = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/corrections/{correctionId}/approve",
            new { reason = "Compared with signed sheet" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK,
            await approved.Content.ReadAsStringAsync());
        var delivered = await correctionBroadcast.Task.WaitAsync(TimeSpan.FromSeconds(5));
        delivered.EventId.Should().Be(scoreId);
        delivered.Value.Should().Be(3);

        var reverseRequest = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}/corrections",
            new { kind = "Reverse", reason = "Event did not occur" });
        reverseRequest.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var reverseJson = JsonDocument.Parse(await reverseRequest.Content.ReadAsStringAsync());
        var reverseId = reverseJson.RootElement.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/corrections/{reverseId}/approve",
            new { reason = "Video review" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var replacementRequest = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}/corrections",
            new
            {
                kind = "Replace",
                reason = "Restore under corrected classification",
                value = 2,
                eventType = "strokes",
                metricName = "strokes",
                participantId,
                segmentId
            });
        replacementRequest.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var replacementJson = JsonDocument.Parse(
            await replacementRequest.Content.ReadAsStringAsync());
        var replacementId = replacementJson.RootElement.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/corrections/{replacementId}/approve",
            new { reason = "Supervisor approval" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var rejectedRequest = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{scoreId}/corrections",
            new { kind = "Correct", reason = "Unverified claim", value = 9 });
        using var rejectedJson = JsonDocument.Parse(
            await rejectedRequest.Content.ReadAsStringAsync());
        var rejectedId = rejectedJson.RootElement.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/corrections/{rejectedId}/reject",
            new { reason = "No supporting evidence" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var finalScores = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/livescoring/contests/{contestId}/scores");
        finalScores!.Single(item => item.GetProperty("id").GetGuid() == scoreId)
            .GetProperty("value").GetDecimal().Should().Be(2);
        var automaticSnapshot = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{contestId}/snapshot");
        automaticSnapshot.GetProperty("state").GetProperty("currentScores")
            .EnumerateArray().Single(item =>
                item.GetProperty("participantId").GetGuid() == participantId
                && item.GetProperty("metricName").GetString() == "strokes")
            .GetProperty("value").GetDecimal().Should().Be(2);
        automaticSnapshot.GetProperty("state").GetProperty("segmentScores")[0]
            .GetProperty("segmentId").GetGuid().Should().Be(segmentId);
        var automaticHash = automaticSnapshot.GetProperty("sourceHash").GetString();
        var explicitRebuild = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/snapshot/rebuild",
            new { reason = "Determinism verification" });
        explicitRebuild.StatusCode.Should().Be(HttpStatusCode.OK,
            await explicitRebuild.Content.ReadAsStringAsync());
        using var explicitSnapshot = JsonDocument.Parse(
            await explicitRebuild.Content.ReadAsStringAsync());
        explicitSnapshot.RootElement.GetProperty("sourceHash").GetString()
            .Should().Be(automaticHash);
        explicitSnapshot.RootElement.GetProperty("version").GetInt32()
            .Should().BeGreaterThan(automaticSnapshot.GetProperty("version").GetInt32());
        var corrections = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/livescoring/contests/{contestId}/corrections");
        corrections!.Where(item => item.GetProperty("liveScoreId").GetGuid() == scoreId)
            .Should().Contain(item => item.GetProperty("status").GetString() == "Rejected")
            .And.Contain(item => item.GetProperty("status").GetString() == "Approved");
        corrections.Should().OnlyContain(item =>
            item.GetProperty("oldValue").ValueKind == JsonValueKind.String &&
            item.GetProperty("reason").GetString() != null);

        var resultId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var result = new ContestResult(
                resultId, contestId, Guid.NewGuid(), participantId);
            result.SetScore(2, "2", null);
            result.SetRank(1, "1");
            result.Submit();
            result.BeginReview();
            result.Approve(Guid.NewGuid());
            result.ReopenForCorrection("Integration correction setup");
            database.ContestResults.Add(result);
            await database.SaveChangesAsync();
        }
        var correctedResult = await client.PostAsJsonAsync(
            $"/api/results/{resultId}/correct",
            new
            {
                score = 5,
                scoreDisplay = "5",
                rank = 2,
                position = "2",
                reason = "Approved scorecard correction"
            });
        correctedResult.StatusCode.Should().Be(HttpStatusCode.OK,
            await correctedResult.Content.ReadAsStringAsync());
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var audit = await database.AuditEntries.SingleAsync(item =>
                item.EntityType == nameof(ContestResult) &&
                item.EntityId == resultId.ToString() &&
                item.Action == "ResultCorrected");
            audit.Reason.Should().Be("Approved scorecard correction");
            audit.OriginalValue.Should().Contain("2");
            audit.NewValue.Should().Contain("5");
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var scoreIds = await database.LiveScores
                .Where(item => item.ContestId == contestId)
                .Select(item => item.Id)
                .ToArrayAsync();
            database.LiveScoreUpdates.RemoveRange(
                database.LiveScoreUpdates.Where(item => scoreIds.Contains(item.LiveScoreId)));
            database.LiveScores.RemoveRange(
                database.LiveScores.Where(item => item.ContestId == contestId));
            database.ContestResults.RemoveRange(
                database.ContestResults.Where(item => item.Id == resultId));
            database.Contests.RemoveRange(
                database.Contests.Where(item => item.Id == contestId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SessionClock_AttributesScoresAndTransitionsSegments()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-session-clock");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={scorer.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contest = contests.RootElement.EnumerateArray()
            .First(item => item.GetProperty("segments").GetArrayLength() > 1);
        var contestId = contest.GetProperty("id").GetGuid();
        var participantId = contest.GetProperty("participants")[0]
            .GetProperty("participantId").GetGuid();
        var secondSegmentId = contest.GetProperty("segments")[1]
            .GetProperty("id").GetGuid();

        var existingSession = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/session");
        if (existingSession.StatusCode == HttpStatusCode.OK)
        {
            await client.PostAsync(
                $"/api/livescoring/contests/{contestId}/session/end",
                null);
        }

        var start = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/session/start",
            new { eventId = Guid.NewGuid(), name = "Clock integration" });
        start.StatusCode.Should().Be(HttpStatusCode.OK);

        var context = await client.PutAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/session/context",
            new { segmentId = secondSegmentId, clockSeconds = 60m });
        context.StatusCode.Should().Be(HttpStatusCode.OK);
        var clockStart = await client.PostAsync(
            $"/api/livescoring/contests/{contestId}/session/clock/start",
            null);
        clockStart.StatusCode.Should().Be(HttpStatusCode.OK);

        var submit = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId,
                metricName = "score",
                value = 1,
                scoredBy = "session-clock-test"
            });
        submit.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await submit.Content.ReadAsStringAsync());

        await client.PostAsync(
            $"/api/livescoring/contests/{contestId}/session/clock/pause",
            null);
        var scoresResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/scores");
        using var scores = JsonDocument.Parse(
            await scoresResponse.Content.ReadAsStringAsync());
        var score = scores.RootElement.EnumerateArray().Last();
        score.GetProperty("segmentId").GetGuid().Should().Be(secondSegmentId);
        score.GetProperty("clockSeconds").GetDecimal().Should().BeGreaterThanOrEqualTo(60m);

        var sessionResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/session");
        using var session = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());
        session.RootElement.GetProperty("isClockRunning").GetBoolean()
            .Should().BeFalse();
        session.RootElement.GetProperty("currentClockSeconds").GetDecimal()
            .Should().BeGreaterThanOrEqualTo(60m);

        await client.PostAsync(
            $"/api/livescoring/contests/{contestId}/session/end",
            null);

    }

    [Fact]
    public async Task NestedSegments_AreExposedAndCanDriveSessionProgression()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@golf.com",
            "Scorer123!",
            "api-integration-nested-segments");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={scorer.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contestId = contests.RootElement.EnumerateArray().First()
            .GetProperty("id").GetGuid();

        var schemaResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/schema");
        schemaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var schema = JsonDocument.Parse(
            await schemaResponse.Content.ReadAsStringAsync());
        var segments = schema.RootElement.GetProperty("segments")
            .EnumerateArray().ToArray();
        var firstRound = segments.First(segment =>
            segment.GetProperty("type").GetString() == "Round");
        var holes = segments.Where(segment =>
                segment.GetProperty("parentSegmentId").ValueKind !=
                JsonValueKind.Null &&
                segment.GetProperty("parentSegmentId").GetGuid() ==
                firstRound.GetProperty("id").GetGuid())
            .ToArray();
        holes.Should().HaveCount(18);
        holes.Should().OnlyContain(segment =>
            segment.GetProperty("depth").GetInt32() == 1 &&
            segment.GetProperty("type").GetString() == "Hole");

        var existingSession = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/session");
        if (existingSession.StatusCode == HttpStatusCode.OK)
        {
            await client.PostAsync(
                $"/api/livescoring/contests/{contestId}/session/end",
                null);
        }

        var start = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/session/start",
            new { eventId = Guid.NewGuid(), name = "Nested segment integration" });
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        using var started = JsonDocument.Parse(
            await start.Content.ReadAsStringAsync());
        started.RootElement.GetProperty("activeSegmentId").GetGuid()
            .Should().Be(holes[0].GetProperty("id").GetGuid());

        var advance = await client.PostAsync(
            $"/api/livescoring/contests/{contestId}/session/segment/next",
            null);
        advance.StatusCode.Should().Be(HttpStatusCode.OK);

        var progressedResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/schema");
        using var progressed = JsonDocument.Parse(
            await progressedResponse.Content.ReadAsStringAsync());
        var progressedSegments = progressed.RootElement.GetProperty("segments")
            .EnumerateArray().ToArray();
        progressedSegments.Single(segment =>
                segment.GetProperty("id").GetGuid() ==
                firstRound.GetProperty("id").GetGuid())
            .GetProperty("status").GetString().Should().Be("InProgress");
        progressedSegments.Single(segment =>
                segment.GetProperty("id").GetGuid() ==
                holes[0].GetProperty("id").GetGuid())
            .GetProperty("status").GetString().Should().Be("Completed");
        progressedSegments.Single(segment =>
                segment.GetProperty("id").GetGuid() ==
                holes[1].GetProperty("id").GetGuid())
            .GetProperty("status").GetString().Should().Be("InProgress");

        await client.PostAsync(
            $"/api/livescoring/contests/{contestId}/session/end",
            null);

        var footballScorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-conditional-segments");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", footballScorer.AccessToken);
        var footballContestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={footballScorer.TenantId}");
        using var footballContests = JsonDocument.Parse(
            await footballContestsResponse.Content.ReadAsStringAsync());
        var footballContestId = footballContests.RootElement
            .EnumerateArray().First().GetProperty("id").GetGuid();
        var footballSchemaResponse = await client.GetAsync(
            $"/api/livescoring/contests/{footballContestId}/schema");
        using var footballSchema = JsonDocument.Parse(
            await footballSchemaResponse.Content.ReadAsStringAsync());
        var optionalSegment = footballSchema.RootElement
            .GetProperty("segments").EnumerateArray()
            .Single(segment => segment.GetProperty("isOptional").GetBoolean());
        optionalSegment.GetProperty("type").GetString().Should().Be("Overtime");
        optionalSegment.GetProperty("activationCondition").GetString()
            .Should().Be("tie_after_regulation");
    }

    [Fact]
    public async Task Scorer_CanCheckInAndIdempotentlySubmitContestResults()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-mobile-workflow");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={scorer.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contest = contests.RootElement.EnumerateArray().First();
        var contestId = contest.GetProperty("id").GetGuid();
        var participantId = contest.GetProperty("participants")[0]
            .GetProperty("participantId").GetGuid();
        var segmentId = contest.GetProperty("segments")[0]
            .GetProperty("id").GetGuid();

        var checkIn = await client.PostAsync(
            $"/api/checkin/contests/{contestId}/participants/{participantId}/checkin",
            null);
        checkIn.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await checkIn.Content.ReadAsStringAsync());
        var repeatedCheckIn = await client.PostAsync(
            $"/api/checkin/contests/{contestId}/participants/{participantId}/checkin",
            null);
        repeatedCheckIn.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkInStatusResponse = await client.GetAsync(
            $"/api/checkin/contests/{contestId}/status");
        using var checkInStatus = JsonDocument.Parse(
            await checkInStatusResponse.Content.ReadAsStringAsync());
        checkInStatus.RootElement.GetProperty("checkedIn").GetInt32()
            .Should().BeGreaterThanOrEqualTo(1);

        var score = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId,
                metricName = "score",
                value = 1,
                scoredBy = "mobile-workflow-test",
                segmentId,
                clockSeconds = 10m
            });
        score.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await score.Content.ReadAsStringAsync());

        var submit = await client.PostAsync(
            $"/api/results/contest/{contestId}/submit",
            null);
        submit.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await submit.Content.ReadAsStringAsync());
        using var submitted = JsonDocument.Parse(
            await submit.Content.ReadAsStringAsync());
        submitted.RootElement.GetProperty("status").GetString()
            .Should().Be("Submitted");
        var result = submitted.RootElement.GetProperty("results")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("participantId").GetGuid() == participantId);
        result.GetProperty("status").GetString().Should().Be("Submitted");
        var resultId = result.GetProperty("id").GetGuid();

        var replay = await client.PostAsync(
            $"/api/results/contest/{contestId}/submit",
            null);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        using var replayed = JsonDocument.Parse(
            await replay.Content.ReadAsStringAsync());
        replayed.RootElement.GetProperty("results").EnumerateArray()
            .Single(item =>
                item.GetProperty("participantId").GetGuid() == participantId)
            .GetProperty("id").GetGuid().Should().Be(resultId);
    }

    [Fact]
    public async Task Admin_CanCreateNamedContestAndAttachTenantParticipantsIdempotently()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-isolated-contest");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var tenants = await client.GetFromJsonAsync<JsonElement[]>("/api/tenants");
        var premierTenantId = tenants!
            .Single(item => item.GetProperty("slug").GetString() == "premier-league")
            .GetProperty("id").GetGuid();
        var nbaTenantId = tenants!
            .Single(item => item.GetProperty("slug").GetString() == "nba")
            .GetProperty("id").GetGuid();
        var premierParticipants = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/participants?tenantId={premierTenantId}");
        var nbaParticipants = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/participants?tenantId={nbaTenantId}");
        var participantId = premierParticipants!
            .First(item => item.GetProperty("type").GetInt32() == 0)
            .GetProperty("id").GetGuid();
        var otherTenantParticipantId = nbaParticipants!
            .First(item => item.GetProperty("type").GetInt32() == 0)
            .GetProperty("id").GetGuid();
        var contestName = $"Isolated Mobile {Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/contests", new
        {
            tenantId = premierTenantId,
            sportId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            contestShape = "HeadToHead",
            name = contestName
        });
        created.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await created.Content.ReadAsStringAsync());
        using var createdPayload = JsonDocument.Parse(
            await created.Content.ReadAsStringAsync());
        var contestId = createdPayload.RootElement.GetProperty("id").GetGuid();
        createdPayload.RootElement.GetProperty("name").GetString()
            .Should().Be(contestName);

        var first = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/participants",
            new { participantId, participantType = "Team" });
        var replay = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/participants",
            new { participantId, participantType = "Team" });
        var crossTenant = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/participants",
            new { participantId = otherTenantParticipantId, participantType = "Team" });

        first.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await first.Content.ReadAsStringAsync());
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        crossTenant.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            await crossTenant.Content.ReadAsStringAsync());
        var contest = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{contestId}");
        contest.GetProperty("participants").EnumerateArray()
            .Should().ContainSingle(item =>
                item.GetProperty("participantId").GetGuid() == participantId);
    }

    [Fact]
    public async Task ApprovedTeams_GenerateDoubleRoundRobinAndUpdatePublicStandingsAcrossMatches()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-football-league");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var premierTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");
        var footballSportId =
            Guid.Parse("00000000-0000-0000-0000-000000000100");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var competitionResponse = await client.PostAsJsonAsync(
            "/api/competitions",
            new
            {
                name = $"Fresh Football League {suffix}",
                sportId = footballSportId,
                tenantId = premierTenantId
            });
        competitionResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        var participantIds = new List<Guid>();
        for (var index = 1; index <= 8; index++)
        {
            var teamResponse = await client.PostAsJsonAsync(
                "/api/participants/teams",
                new
                {
                    name = $"League {suffix} Team {index}",
                    shortName = $"L{index}{suffix[..3]}",
                    tenantId = premierTenantId
                });
            teamResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await teamResponse.Content.ReadAsStringAsync());
            var team = await teamResponse.Content.ReadFromJsonAsync<JsonElement>();
            var participantId = team.GetProperty("id").GetGuid();
            participantIds.Add(participantId);

            var registrationResponse = await client.PostAsJsonAsync(
                $"/api/registrations/competition/{competitionId}",
                new { participantId, type = 1 });
            registrationResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registration = await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var registrationId = registration.GetProperty("id").GetGuid();

            (await client.PostAsync(
                $"/api/registrations/{registrationId}/submit",
                null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsJsonAsync(
                $"/api/registrations/{registrationId}/approve",
                new { reviewerId = Guid.NewGuid(), notes = "Eligible" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var fixturesResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/fixtures/round-robin",
            new
            {
                editionName = "2026/27",
                stageName = "League",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                venue = "Integration Stadium",
                homeAndAway = true,
                daysBetweenRounds = 1,
                drawSeed = 20260811
            });
        fixturesResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await fixturesResponse.Content.ReadAsStringAsync());
        var fixturePayload = await fixturesResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        fixturePayload.GetProperty("eligibleParticipantCount").GetInt32()
            .Should().Be(8);
        fixturePayload.GetProperty("fixtureCount").GetInt32()
            .Should().Be(56);
        fixturePayload.GetProperty("drawSeed").GetInt32()
            .Should().Be(20260811);
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var storedSeed = await database.Competitions
                .Where(item => item.Id == competitionId)
                .SelectMany(item => item.Editions)
                .SelectMany(item => item.Stages)
                .Select(item => item.FixtureGenerationSeed)
                .SingleAsync();
            storedSeed.Should().Be(20260811);
        }
        var fixtures = fixturePayload.GetProperty("fixtures")
            .EnumerateArray()
            .ToArray();
        var firstFootballContestId = fixtures[0].GetProperty("contestId").GetGuid();
        var firstFootballContest = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{firstFootballContestId}");
        firstFootballContest.GetProperty("segments").EnumerateArray()
            .Select(segment => segment.GetProperty("name").GetString())
            .Should().ContainInOrder(
                "First Half", "Second Half", "Extra Time", "Penalty Shootout");
        var footballSchema = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{firstFootballContestId}/schema");
        footballSchema.GetProperty("actions").EnumerateArray()
            .Select(action => action.GetProperty("code").GetString())
            .Should().Contain(new[]
            {
                "goal", "own-goal", "penalty-goal", "yellow-card", "red-card",
                "substitution-in", "penalty-scored", "penalty-missed"
            });
        fixtures.Select(fixture =>
                string.Join(
                    ":",
                    new[]
                    {
                        fixture.GetProperty("homeParticipantId").GetGuid(),
                        fixture.GetProperty("awayParticipantId").GetGuid()
                    }.Order()))
            .GroupBy(pair => pair)
            .Should().OnlyContain(pair => pair.Count() == 2);

        var publishTournament = await client.PostAsync(
            $"/api/competitions/{competitionId}/publish",
            null);
        publishTournament.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            await publishTournament.Content.ReadAsStringAsync());

        for (var matchIndex = 0; matchIndex < 4; matchIndex++)
        {
            var fixture = fixtures[matchIndex];
            var contestId = fixture.GetProperty("contestId").GetGuid();
            var homeId = fixture.GetProperty("homeParticipantId").GetGuid();
            var awayId = fixture.GetProperty("awayParticipantId").GetGuid();
            foreach (var scorerId in new[] { homeId, homeId, awayId })
            {
                var scoreResponse = await client.PostAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/events",
                    new
                    {
                        eventId = Guid.NewGuid(),
                        participantId = scorerId,
                        metricName = "score",
                        value = 1,
                        scoredBy = "football-league-integration"
                    });
                scoreResponse.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await scoreResponse.Content.ReadAsStringAsync());
            }

            var submitResponse = await client.PostAsync(
                $"/api/results/contest/{contestId}/submit",
                null);
            submitResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await submitResponse.Content.ReadAsStringAsync());
            var submitted = await submitResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            foreach (var result in submitted.GetProperty("results").EnumerateArray())
            {
                var approval = await client.PostAsJsonAsync(
                    $"/api/results/{result.GetProperty("id").GetGuid()}/approve",
                    new { approvedBy = Guid.NewGuid() });
                approval.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await approval.Content.ReadAsStringAsync());
            }

            var standings = await client.GetFromJsonAsync<JsonElement[]>(
                $"/api/public/tenants/premier-league/standings"
                + $"?competitionId={competitionId}");
            standings!.Sum(row => row.GetProperty("played").GetInt32())
                .Should().Be((matchIndex + 1) * 2);
        }

        var leagueStandings = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/public/tenants/premier-league/standings"
            + $"?competitionId={competitionId}");
        leagueStandings.Should().HaveCount(8);
        using (var footballMetadata = JsonDocument.Parse(
                   leagueStandings![0].GetProperty("metadata").GetString()!))
        {
            footballMetadata.RootElement.GetProperty("goalsScored").GetInt32()
                .Should().Be(leagueStandings[0].GetProperty("pointsFor").GetInt32());
            footballMetadata.RootElement.TryGetProperty("goalDifference", out _).Should().BeTrue();
            footballMetadata.RootElement.TryGetProperty("headToHeadPoints", out _).Should().BeTrue();
        }

        var playoffsResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/top-four",
            new
            {
                bracketName = "Championship Playoffs",
                stageName = "Top Four",
                startTimeUtc = DateTime.UtcNow.AddDays(30),
                venue = "Finals Stadium",
                daysUntilFinal = 7
            });
        playoffsResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await playoffsResponse.Content.ReadAsStringAsync());
        var playoffs = await playoffsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var semiFinalContestIds = playoffs
            .GetProperty("semiFinalContestIds")
            .EnumerateArray()
            .Select(item => item.GetGuid())
            .ToArray();
        semiFinalContestIds.Should().HaveCount(2);
        var finalContestId = playoffs.GetProperty("finalContestId").GetGuid();

        async Task<Guid> ScoreAndApproveAsync(Guid contestId)
        {
            var contest = await client.GetFromJsonAsync<JsonElement>(
                $"/api/contests/{contestId}");
            var matchParticipants = contest.GetProperty("participants")
                .EnumerateArray()
                .Select(item => item.GetProperty("participantId").GetGuid())
                .ToArray();
            matchParticipants.Should().HaveCount(2);
            foreach (var scorerId in new[]
                     {
                         matchParticipants[0],
                         matchParticipants[0],
                         matchParticipants[1]
                     })
            {
                var scored = await client.PostAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/events",
                    new
                    {
                        eventId = Guid.NewGuid(),
                        participantId = scorerId,
                        metricName = "score",
                        value = 1,
                        scoredBy = "football-playoff-integration"
                    });
                scored.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await scored.Content.ReadAsStringAsync());
            }

            var submittedResponse = await client.PostAsync(
                $"/api/results/contest/{contestId}/submit",
                null);
            submittedResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await submittedResponse.Content.ReadAsStringAsync());
            var submitted = await submittedResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            foreach (var result in submitted.GetProperty("results").EnumerateArray())
            {
                var approval = await client.PostAsJsonAsync(
                    $"/api/results/{result.GetProperty("id").GetGuid()}/approve",
                    new { approvedBy = Guid.NewGuid() });
                approval.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await approval.Content.ReadAsStringAsync());
            }
            return matchParticipants[0];
        }

        var semiFinalWinners = new List<Guid>();
        foreach (var semiFinalContestId in semiFinalContestIds)
            semiFinalWinners.Add(await ScoreAndApproveAsync(semiFinalContestId));

        var populatedFinal = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{finalContestId}");
        populatedFinal.GetProperty("participants")
            .EnumerateArray()
            .Select(item => item.GetProperty("participantId").GetGuid())
            .Should().BeEquivalentTo(semiFinalWinners);

        var championId = await ScoreAndApproveAsync(finalContestId);
        var publicPlayoffs = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/premier-league/playoffs"
            + $"?competitionId={competitionId}");
        publicPlayoffs.GetProperty("status").GetString()
            .Should().Be("Completed");
        publicPlayoffs.GetProperty("championId").GetGuid()
            .Should().Be(championId);
        publicPlayoffs.GetProperty("rounds").GetArrayLength()
            .Should().Be(2);
    }

    [Fact]
    public async Task BasketballScoring_AttributesEventsToRosterPlayersAndTeamTotals()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-basketball-player-scoring");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var basketballSportId =
            Guid.Parse("00000000-0000-0000-0000-000000000101");
        var teamId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var unrelatedAthleteId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        Guid quarterId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var team = new Team(teamId, admin.TenantId, "Roster Basketball Home", "RBH");
            var roster = team.AddRoster("Active basketball roster", null);
            roster.AddAthlete(athleteId, "23", "Guard");
            var opponent = new Team(opponentId, admin.TenantId, "Roster Basketball Away", "RBA");
            var athlete = new Athlete(athleteId, admin.TenantId, "Jordan Player", "Jordan");
            var unrelatedAthlete = new Athlete(
                unrelatedAthleteId, admin.TenantId, "Unrelated Player", "Unrelated");
            var contest = new Contest(contestId, admin.TenantId, basketballSportId, "TeamVsTeam");
            contest.SetName("Basketball player attribution verification");
            contest.AddParticipant(teamId, "Team", 1);
            contest.AddParticipant(opponentId, "Team", 2);
            quarterId = contest.AddSegment("Quarter 1", "Quarter", 1).Id;
            database.Participants.AddRange(team, opponent, athlete, unrelatedAthlete);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        var rosterResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/rosters");
        rosterResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await rosterResponse.Content.ReadAsStringAsync());
        var rosterPayload = await rosterResponse.Content.ReadFromJsonAsync<JsonElement>();
        var homeRoster = rosterPayload.EnumerateArray()
            .Single(item => item.GetProperty("teamId").GetGuid() == teamId);
        var player = homeRoster.GetProperty("players").EnumerateArray().Single();
        player.GetProperty("athleteId").GetGuid().Should().Be(athleteId);
        player.GetProperty("jerseyNumber").GetString().Should().Be("23");

        object Event(Guid secondaryParticipantId) => new
        {
            eventId = Guid.NewGuid(),
            participantId = teamId,
            secondaryParticipantId,
            eventType = "THREE_POINTER_MADE",
            metricName = "score",
            value = 3,
            segmentId = quarterId,
            scoredBy = "basketball-player-integration"
        };
        var rejected = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Event(unrelatedAthleteId));
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should()
            .Contain("active roster");

        var accepted = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Event(athleteId));
        accepted.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await accepted.Content.ReadAsStringAsync());
        var acceptedPayload = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        var persisted = acceptedPayload.GetProperty("event");
        persisted.GetProperty("participantId").GetGuid().Should().Be(teamId);
        persisted.GetProperty("secondaryParticipantId").GetGuid().Should().Be(athleteId);
        persisted.GetProperty("value").GetDecimal().Should().Be(3);
    }

    [Fact]
    public async Task BasketballLeague_CompletesRegularSeasonBestOfThreeSemifinalsAndOvertimeFinal()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-basketball-championship");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var configuredRules = await client.PostAsJsonAsync(
            "/api/sports/basketball/configure",
            new { createdBy = "basketball-package-integration" });
        configuredRules.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await configuredRules.Content.ReadAsStringAsync());
        var configuredVersion = await configuredRules.Content
            .ReadFromJsonAsync<JsonElement>();
        var currentRules = await client.GetFromJsonAsync<JsonElement>(
            "/api/sports/basketball/ruleset");
        currentRules.GetProperty("id").GetGuid().Should()
            .Be(configuredVersion.GetProperty("id").GetGuid());

        var tenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000003");
        var sportId =
            Guid.Parse("00000000-0000-0000-0000-000000000101");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var competitionResponse = await client.PostAsJsonAsync(
            "/api/competitions",
            new
            {
                name = $"Fresh Basketball League {suffix}",
                sportId,
                tenantId
            });
        competitionResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        for (var index = 1; index <= 6; index++)
        {
            var teamResponse = await client.PostAsJsonAsync(
                "/api/participants/teams",
                new
                {
                    name = $"Basketball {suffix} Team {index}",
                    shortName = $"B{index}{suffix[..3]}",
                    tenantId
                });
            teamResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await teamResponse.Content.ReadAsStringAsync());
            var team = await teamResponse.Content.ReadFromJsonAsync<JsonElement>();
            var registrationResponse = await client.PostAsJsonAsync(
                $"/api/registrations/competition/{competitionId}",
                new
                {
                    participantId = team.GetProperty("id").GetGuid(),
                    type = 1
                });
            registrationResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registration = await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var registrationId = registration.GetProperty("id").GetGuid();
            (await client.PostAsync(
                $"/api/registrations/{registrationId}/submit",
                null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsJsonAsync(
                $"/api/registrations/{registrationId}/approve",
                new { reviewerId = Guid.NewGuid(), notes = "Eligible" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var fixtureResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/fixtures/round-robin",
            new
            {
                editionName = "2026/27",
                stageName = "Regular Season",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                venue = "Integration Arena",
                homeAndAway = false,
                daysBetweenRounds = 1
            });
        fixtureResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await fixtureResponse.Content.ReadAsStringAsync());
        var fixturePayload = await fixtureResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        fixturePayload.GetProperty("fixtureCount").GetInt32().Should().Be(15);
        var fixtures = fixturePayload.GetProperty("fixtures")
            .EnumerateArray()
            .ToArray();

        var validationContestId = fixtures[0].GetProperty("contestId").GetGuid();
        var validationContest = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{validationContestId}");
        validationContest.GetProperty("rulesetVersionId").GetGuid().Should()
            .Be(configuredVersion.GetProperty("id").GetGuid());
        var validationParticipantId = validationContest.GetProperty("participants")[0]
            .GetProperty("participantId").GetGuid();
        var validationSegmentId = validationContest.GetProperty("segments")[0]
            .GetProperty("id").GetGuid();
        var invalidPointValue = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{validationContestId}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId = validationParticipantId,
                eventType = "FIELD_GOAL_MADE",
                metricName = "score",
                value = 4,
                scoredBy = "basketball-validation",
                segmentId = validationSegmentId,
                clockSeconds = 720m
            });
        invalidPointValue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var mismatchedPointEvent = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{validationContestId}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId = validationParticipantId,
                eventType = "THREE_POINTER_MADE",
                metricName = "score",
                value = 1,
                scoredBy = "basketball-validation",
                segmentId = validationSegmentId,
                clockSeconds = 720m
            });
        mismatchedPointEvent.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsync(
            $"/api/competitions/{competitionId}/publish",
            null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        async Task<Guid> ScoreAndApproveAsync(
            Guid contestId,
            bool useOvertime = false,
            Guid? desiredWinnerId = null)
        {
            var contest = await client.GetFromJsonAsync<JsonElement>(
                $"/api/contests/{contestId}");
            var participants = contest.GetProperty("participants")
                .EnumerateArray()
                .Select(item => item.GetProperty("participantId").GetGuid())
                .ToArray();
            participants.Should().HaveCount(2);
            var winnerId = desiredWinnerId ?? participants[0];
            var loserId = participants.Single(id => id != winnerId);
            var segments = contest.GetProperty("segments")
                .EnumerateArray()
                .OrderBy(item => item.GetProperty("sortOrder").GetInt32())
                .ToArray();
            segments.Should().HaveCount(5);
            var scoringSegmentId = segments[0].GetProperty("id").GetGuid();

            if (useOvertime)
            {
                var sessionStart = await client.PostAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/session/start",
                    new
                    {
                        eventId = Guid.NewGuid(),
                        name = "Basketball overtime"
                    });
                sessionStart.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await sessionStart.Content.ReadAsStringAsync());
                for (var quarter = 1; quarter < 4; quarter++)
                {
                    var next = await client.PostAsync(
                        $"/api/livescoring/contests/{contestId}/session/segment/next",
                        null);
                    next.StatusCode.Should().Be(
                        HttpStatusCode.OK,
                        await next.Content.ReadAsStringAsync());
                }
                scoringSegmentId = segments[3].GetProperty("id").GetGuid();
                var prematureOvertime = await client.PutAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/session/context",
                    new
                    {
                        segmentId = segments[4].GetProperty("id").GetGuid(),
                        clockSeconds = 300m
                    });
                prematureOvertime.StatusCode.Should()
                    .Be(HttpStatusCode.BadRequest,
                        await prematureOvertime.Content.ReadAsStringAsync());
            }

            foreach (var score in new[]
                     {
                         (ParticipantId: winnerId, Value: 2m),
                         (ParticipantId: loserId, Value: useOvertime ? 2m : 1m)
                     })
            {
                var scored = await client.PostAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/events",
                    new
                    {
                        eventId = Guid.NewGuid(),
                        participantId = score.ParticipantId,
                        metricName = "score",
                        value = score.Value,
                        scoredBy = "basketball-championship-integration",
                        segmentId = scoringSegmentId,
                        clockSeconds = 0m
                    });
                scored.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await scored.Content.ReadAsStringAsync());
            }

            if (useOvertime)
            {
                var overtimeId = segments[4].GetProperty("id").GetGuid();
                var overtime = await client.PutAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/session/context",
                    new { segmentId = overtimeId, clockSeconds = 300m });
                overtime.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await overtime.Content.ReadAsStringAsync());
                var winningPoint = await client.PostAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/events",
                    new
                    {
                        eventId = Guid.NewGuid(),
                        participantId = winnerId,
                        metricName = "score",
                        value = 1,
                        scoredBy = "basketball-overtime-integration",
                        segmentId = overtimeId,
                        clockSeconds = 1m
                    });
                winningPoint.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await winningPoint.Content.ReadAsStringAsync());
            }

            var submission = await client.PostAsync(
                $"/api/results/contest/{contestId}/submit",
                null);
            submission.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await submission.Content.ReadAsStringAsync());
            var submitted = await submission.Content
                .ReadFromJsonAsync<JsonElement>();
            foreach (var result in submitted.GetProperty("results").EnumerateArray())
            {
                var approval = await client.PostAsJsonAsync(
                    $"/api/results/{result.GetProperty("id").GetGuid()}/approve",
                    new { approvedBy = Guid.NewGuid() });
                approval.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await approval.Content.ReadAsStringAsync());
            }
            return winnerId;
        }

        foreach (var fixture in fixtures)
        {
            await ScoreAndApproveAsync(
                fixture.GetProperty("contestId").GetGuid());
        }
        var standings = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/public/tenants/nba/standings?competitionId={competitionId}");
        standings.Should().HaveCount(6);
        standings.Should().OnlyContain(row =>
            row.GetProperty("played").GetInt32() == 5);

        var playoffsResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/top-four",
            new
            {
                bracketName = "Basketball Championship",
                stageName = "Top Four",
                startTimeUtc = DateTime.UtcNow.AddDays(30),
                venue = "Finals Arena",
                daysUntilFinal = 10,
                semifinalBestOf = 3,
                finalBestOf = 1,
                daysBetweenSeriesGames = 2
            });
        playoffsResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await playoffsResponse.Content.ReadAsStringAsync());
        var playoffs = await playoffsResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var semifinalSeries = playoffs.GetProperty("semiFinalSeries")
            .EnumerateArray()
            .ToArray();
        semifinalSeries.Should().HaveCount(2);
        var semifinalWinners = new List<Guid>();
        var overtimeHandled = false;
        foreach (var series in semifinalSeries)
        {
            var games = series.GetProperty("contestIds")
                .EnumerateArray()
                .Select(item => item.GetGuid())
                .ToArray();
            games.Should().HaveCount(3);
            var seriesWinner = await ScoreAndApproveAsync(
                games[0],
                useOvertime: !overtimeHandled);
            semifinalWinners.Add(seriesWinner);
            overtimeHandled = true;
            await ScoreAndApproveAsync(
                games[1],
                desiredWinnerId: seriesWinner);
            var unneededGame = await client.GetFromJsonAsync<JsonElement>(
                $"/api/contests/{games[2]}");
            unneededGame.GetProperty("status").GetInt32()
                .Should().Be(12);
        }

        var finalContestId = playoffs.GetProperty("finalSeries")
            .GetProperty("contestIds")[0].GetGuid();
        var populatedFinal = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{finalContestId}");
        populatedFinal.GetProperty("participants")
            .EnumerateArray()
            .Select(item => item.GetProperty("participantId").GetGuid())
            .Should().BeEquivalentTo(semifinalWinners);
        var championId = await ScoreAndApproveAsync(finalContestId);

        var publicPlayoffs = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/nba/playoffs?competitionId={competitionId}");
        publicPlayoffs.GetProperty("status").GetString().Should().Be("Completed");
        publicPlayoffs.GetProperty("championId").GetGuid().Should().Be(championId);
        publicPlayoffs.GetProperty("rounds")[0]
            .GetProperty("matches")[0]
            .GetProperty("seriesGames")
            .GetArrayLength()
            .Should().Be(3);
        publicPlayoffs.GetProperty("rounds")[0]
            .GetProperty("matches")[0]
            .GetProperty("seriesGames")[0]
            .TryGetProperty("homeScore", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GolfScorecardReview_RemainsPendingUntilApprovalPublishesEveryHole()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-golf-scorecard-review");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var golfSportId = Guid.Parse("00000000-0000-0000-0000-000000000102");
        var golferId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var golfer = new Athlete(golferId, admin.TenantId, "Review Golfer", "RG");
            var contest = new Contest(
                contestId, admin.TenantId, golfSportId, "MultiRoundLeaderboard");
            contest.SetName("Pending scorecard verification");
            contest.ConfigureLeaderboard(
                72, 2, 1, "Review Course", "Nairobi",
                GolfRulesetService.StandardHolePars);
            contest.AddParticipant(golferId, "Athlete");
            for (var roundNumber = 1; roundNumber <= 4; roundNumber++)
            {
                var round = contest.AddSegment($"Round {roundNumber}", "Round", roundNumber);
                for (var holeNumber = 1; holeNumber <= 18; holeNumber++)
                    round.AddNestedSegment($"Hole {holeNumber}", "Hole", holeNumber);
            }
            database.Participants.Add(golfer);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        var holes = GolfRulesetService.StandardHolePars
            .Select((par, index) => new
            {
                holeNumber = index + 1,
                strokes = par,
                penalties = index == 0 ? 1 : 0
            })
            .ToArray();
        var submitted = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/golf-scorecards",
            new { participantId = golferId, roundNumber = 1, holes });
        submitted.StatusCode.Should().Be(
            HttpStatusCode.Accepted,
            await submitted.Content.ReadAsStringAsync());
        var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var reviewId = pending.GetProperty("id").GetGuid();
        pending.GetProperty("status").GetInt32().Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            (await database.LiveScores.CountAsync(item => item.ContestId == contestId))
                .Should().Be(0);
        }
        var queue = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{contestId}/golf-scorecards");
        queue.GetArrayLength().Should().Be(1);

        var approved = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/golf-scorecards/{reviewId}/approve",
            new { notes = "Signed scorecard checked" });
        approved.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await approved.Content.ReadAsStringAsync());
        var approvedPayload = await approved.Content.ReadFromJsonAsync<JsonElement>();
        approvedPayload.GetProperty("status").GetInt32().Should().Be(1);

        var scores = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{contestId}/scores");
        scores.GetArrayLength().Should().Be(19);
        scores.EnumerateArray().Count(item =>
            item.GetProperty("metricName").GetString() == "strokes").Should().Be(18);
        scores.EnumerateArray().Single(item =>
            item.GetProperty("metricName").GetString() == "penalties")
            .GetProperty("value").GetDecimal().Should().Be(1);
    }

    [Fact]
    public async Task GolfTournament_AppliesTiedCutAndProducesFourRoundLeaderboard()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-golf-tournament");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var configuredRules = await client.PostAsJsonAsync(
            "/api/sports/golf/configure",
            new { createdBy = "golf-package-integration" });
        configuredRules.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await configuredRules.Content.ReadAsStringAsync());
        var configuredVersion = await configuredRules.Content
            .ReadFromJsonAsync<JsonElement>();
        var currentRules = await client.GetFromJsonAsync<JsonElement>(
            "/api/sports/golf/ruleset");
        currentRules.GetProperty("id").GetGuid().Should()
            .Be(configuredVersion.GetProperty("id").GetGuid());

        var tenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000006");
        var sportId =
            Guid.Parse("00000000-0000-0000-0000-000000000102");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var competitionResponse = await client.PostAsJsonAsync(
            "/api/competitions",
            new
            {
                name = $"Fresh Golf Championship {suffix}",
                sportId,
                tenantId
            });
        competitionResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        var golfers = new List<(Guid Id, string Name, int ScoreModifier)>();
        for (var index = 0; index < 24; index++)
        {
            var name = $"Golfer {suffix} {index + 1:D2}";
            var athleteResponse = await client.PostAsJsonAsync(
                "/api/participants/athletes",
                new
                {
                    name,
                    shortName = $"G{index + 1:D2}{suffix[..2]}",
                    tenantId
                });
            athleteResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await athleteResponse.Content.ReadAsStringAsync());
            var athlete = await athleteResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var participantId = athlete.GetProperty("id").GetGuid();
            golfers.Add((participantId, name, index == 12 ? 11 : index));

            var registrationResponse = await client.PostAsJsonAsync(
                $"/api/registrations/competition/{competitionId}",
                new { participantId, type = 0 });
            registrationResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registration = await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var registrationId = registration.GetProperty("id").GetGuid();
            (await client.PostAsync(
                $"/api/registrations/{registrationId}/submit",
                null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsJsonAsync(
                $"/api/registrations/{registrationId}/approve",
                new { reviewerId = Guid.NewGuid(), notes = "Eligible golfer" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var tournamentResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/golf/tournament",
            new
            {
                tournamentName = $"Four Round Championship {suffix}",
                editionName = "2026",
                stageName = "Championship",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                venue = "Integration Golf Club",
                expectedParticipants = 24,
                totalRounds = 4,
                holesPerRound = 18,
                parPerRound = 72,
                cutAfterRound = 2,
                cutToParticipants = 12,
                courseName = "Integration Championship Course",
                courseLocation = "Nairobi, Kenya",
                holePars = GolfRulesetService.StandardHolePars
            });
        tournamentResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await tournamentResponse.Content.ReadAsStringAsync());
        var tournament = await tournamentResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        tournament.GetProperty("participantCount").GetInt32().Should().Be(24);
        tournament.GetProperty("courseName").GetString().Should()
            .Be("Integration Championship Course");
        tournament.GetProperty("holePars").GetArrayLength().Should().Be(18);
        var contestId = tournament.GetProperty("contestId").GetGuid();
        var contest = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{contestId}");
        var rounds = contest.GetProperty("segments").EnumerateArray().ToArray();
        rounds.Should().HaveCount(4);
        rounds.Should().OnlyContain(round =>
            round.GetProperty("nestedSegments").GetArrayLength() == 18);
        contest.GetProperty("courseName").GetString().Should()
            .Be("Integration Championship Course");
        contest.GetProperty("courseLocation").GetString().Should().Be("Nairobi, Kenya");
        contest.GetProperty("rulesetVersionId").GetGuid().Should()
            .Be(configuredVersion.GetProperty("id").GetGuid());
        JsonSerializer.Deserialize<int[]>(contest.GetProperty("holeParsJson").GetString()!)
            .Should().Equal(GolfRulesetService.StandardHolePars);

        (await client.PostAsync(
            $"/api/competitions/{competitionId}/publish",
            null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        static object[] Scorecard(int modifier) =>
            Enumerable.Range(1, 18)
                .Select(hole => (object)new
                {
                    holeNumber = hole,
                    strokes = 4
                        + (modifier / 18)
                        + (hole <= modifier % 18 ? 1 : 0),
                    penalties = 0
                })
                .ToArray();

        async Task SubmitRoundAsync(
            (Guid Id, string Name, int ScoreModifier) golfer,
            int round)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/golf/scorecards",
                new
                {
                    participantId = golfer.Id,
                    roundNumber = round,
                    holes = Scorecard(golfer.ScoreModifier)
                });
            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await response.Content.ReadAsStringAsync());
        }

        foreach (var golfer in golfers)
        {
            await SubmitRoundAsync(golfer, 1);
            await SubmitRoundAsync(golfer, 2);
        }
        var prematureRoundThree = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/golf/scorecards",
            new
            {
                participantId = golfers[0].Id,
                roundNumber = 3,
                holes = Scorecard(golfers[0].ScoreModifier)
            });
        prematureRoundThree.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var cutResponse = await client.PostAsync(
            $"/api/competitions/{competitionId}/golf/cut",
            null);
        cutResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await cutResponse.Content.ReadAsStringAsync());
        var cut = await cutResponse.Content.ReadFromJsonAsync<JsonElement>();
        cut.GetProperty("requestedCutSize").GetInt32().Should().Be(12);
        cut.GetProperty("qualifiedCount").GetInt32().Should().Be(13);
        var qualifiedIds = cut.GetProperty("qualifiedParticipantIds")
            .EnumerateArray()
            .Select(item => item.GetGuid())
            .ToHashSet();
        (await client.PostAsJsonAsync(
            $"/api/checkin/contests/{contestId}/participants/{golfers[23].Id}/withdraw",
            new { reason = "Injury" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(
            $"/api/checkin/contests/{contestId}/participants/{golfers[22].Id}/disqualify",
            new { reason = "Scorecard violation" })).StatusCode.Should()
            .Be(HttpStatusCode.OK);

        foreach (var golfer in golfers.Where(item => qualifiedIds.Contains(item.Id)))
        {
            await SubmitRoundAsync(golfer, 3);
            await SubmitRoundAsync(golfer, 4);
        }

        var leaderboard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/pga-tour/golf/leaderboard"
            + $"?competitionId={competitionId}");
        leaderboard.GetProperty("courseName").GetString().Should()
            .Be("Integration Championship Course");
        leaderboard.GetProperty("holePars").GetArrayLength().Should().Be(18);
        leaderboard.GetProperty("status").GetString().Should().Be("Completed");
        leaderboard.GetProperty("entries").GetArrayLength().Should().Be(24);
        var entries = leaderboard.GetProperty("entries").EnumerateArray().ToArray();
        entries.Count(item => item.GetProperty("madeCut").ValueKind == JsonValueKind.True)
            .Should().Be(13);
        entries[0].GetProperty("name").GetString().Should().Be(golfers[0].Name);
        entries.Where(item => item.GetProperty("isTied").GetBoolean())
            .Should().NotBeEmpty();
        entries.Single(item =>
                item.GetProperty("participantId").GetGuid() == golfers[23].Id)
            .GetProperty("status").GetString().Should().Be("Withdrawn");
        entries.Single(item =>
                item.GetProperty("participantId").GetGuid() == golfers[22].Id)
            .GetProperty("status").GetString().Should().Be("Disqualified");
        entries.Single(item =>
                item.GetProperty("participantId").GetGuid() == golfers[12].Id)
            .GetProperty("position").GetInt32()
            .Should().Be(entries.Single(item =>
                    item.GetProperty("participantId").GetGuid() == golfers[11].Id)
                .GetProperty("position").GetInt32());
        entries[0].GetProperty("roundPositions").EnumerateArray()
            .Should().OnlyContain(position => position.GetInt32() == 1);
        entries[0].GetProperty("todayToPar").GetInt32().Should().Be(0);
        entries[0].GetProperty("thru").GetInt32().Should().Be(18);

        var liveScores = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{contestId}/scores");
        var scoreToCorrect = liveScores.EnumerateArray().First(score =>
            score.GetProperty("participantId").GetGuid() == golfers[0].Id
            && score.GetProperty("metricName").GetString() == "strokes");
        var correctionResponse = await client.PutAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/"
            + scoreToCorrect.GetProperty("id").GetGuid(),
            new
            {
                newValue = 20,
                updatedBy = "golf-integration-admin",
                reason = "Signed scorecard correction"
            });
        correctionResponse.StatusCode.Should().Be(
            HttpStatusCode.Accepted,
            await correctionResponse.Content.ReadAsStringAsync());
        using (var correctionPayload = JsonDocument.Parse(
                   await correctionResponse.Content.ReadAsStringAsync()))
        {
            var correctionId = correctionPayload.RootElement.GetProperty("id").GetGuid();
            var approval = await client.PostAsJsonAsync(
                $"/api/livescoring/contests/{contestId}/corrections/{correctionId}/approve",
                new { reason = "Signed scorecard approved" });
            approval.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await approval.Content.ReadAsStringAsync());
        }

        var correctedLeaderboard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/pga-tour/golf/leaderboard"
            + $"?competitionId={competitionId}");
        var correctedEntries = correctedLeaderboard.GetProperty("entries")
            .EnumerateArray().ToArray();
        correctedEntries[0].GetProperty("participantId").GetGuid()
            .Should().Be(golfers[1].Id);
        correctedEntries.Single(item =>
                item.GetProperty("participantId").GetGuid() == golfers[0].Id)
            .GetProperty("roundPositions")[0].GetInt32().Should().BeGreaterThan(1);

        var correctionHistory = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{contestId}/scores/history");
        correctionHistory.EnumerateArray().Should().Contain(item =>
            item.GetProperty("updateType").GetString() == "CorrectionRequested" &&
            item.GetProperty("newValue").GetString()!
                .Contains("Signed scorecard correction"));
    }

    [Fact]
    public async Task CombatEvent_RunsEightBoutCardWithOfficialsAndPublicResults()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-combat-card");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var rulesetResponse = await client.PostAsJsonAsync(
            "/api/sports/combat/configure",
            new { createdBy = "api-integration-combat-card" });
        rulesetResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await rulesetResponse.Content.ReadAsStringAsync());
        var configuredRuleset = await rulesetResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var rulesetVersionId = configuredRuleset.GetProperty("id").GetGuid();
        var currentRuleset = await client.GetFromJsonAsync<JsonElement>(
            "/api/sports/combat/ruleset");
        currentRuleset.GetProperty("id").GetGuid().Should().Be(rulesetVersionId);

        var tenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000007");
        var sportId =
            Guid.Parse("00000000-0000-0000-0000-000000000105");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var competitionResponse = await client.PostAsJsonAsync(
            "/api/competitions",
            new
            {
                name = $"Fresh Combat Night {suffix}",
                sportId,
                tenantId
            });
        competitionResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        var fighters = new List<(Guid Id, string Name)>();
        for (var index = 0; index < 16; index++)
        {
            var name = $"Fighter {suffix} {index + 1:D2}";
            var athleteResponse = await client.PostAsJsonAsync(
                "/api/participants/athletes",
                new
                {
                    name,
                    shortName = $"F{index + 1:D2}{suffix[..2]}",
                    tenantId
                });
            athleteResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await athleteResponse.Content.ReadAsStringAsync());
            var athlete = await athleteResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var participantId = athlete.GetProperty("id").GetGuid();
            fighters.Add((participantId, name));

            var registrationResponse = await client.PostAsJsonAsync(
                $"/api/registrations/competition/{competitionId}",
                new { participantId, type = 0 });
            registrationResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registration = await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var registrationId = registration.GetProperty("id").GetGuid();
            (await client.PostAsync(
                $"/api/registrations/{registrationId}/submit",
                null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsJsonAsync(
                $"/api/registrations/{registrationId}/approve",
                new { reviewerId = Guid.NewGuid(), notes = "Eligible fighter" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var officialSets = Enumerable.Range(0, 8)
            .Select(bout => Enumerable.Range(0, 4)
                .Select(official => new
                {
                    officialId = Guid.NewGuid(),
                    displayName = official == 0
                        ? $"Referee {bout + 1}"
                        : $"Judge {bout + 1}-{official}",
                    role = official == 0 ? "Referee" : "Judge"
                })
                .ToArray())
            .ToArray();
        var weightClasses = new[]
        {
            "Flyweight", "Bantamweight", "Featherweight", "Lightweight",
            "Welterweight", "Middleweight", "Light Heavyweight", "Heavyweight"
        };
        var cardRequest = new
        {
            editionName = "2026",
            stageName = "Fight Card",
            startTimeUtc = DateTime.UtcNow.AddDays(1),
            venue = "Integration Combat Arena",
            minutesBetweenBouts = 30,
            allowedResultMethods = new[]
            {
                "Unanimous Decision", "Knockout", "Submission"
            },
            bouts = Enumerable.Range(0, 8).Select(index => new
            {
                fighter1Id = fighters[index * 2].Id,
                fighter2Id = fighters[index * 2 + 1].Id,
                weightClass = weightClasses[index],
                cardSection = index switch
                {
                    < 4 => "PreliminaryCard",
                    < 6 => "MainCard",
                    6 => "CoMainEvent",
                    _ => "MainEvent"
                },
                scheduledRounds = index == 7 ? 5 : 3,
                officials = officialSets[index]
            }).ToArray()
        };
        var cardResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/card",
            cardRequest);
        cardResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await cardResponse.Content.ReadAsStringAsync());
        var createdCard = await cardResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        createdCard.GetProperty("boutCount").GetInt32().Should().Be(8);
        createdCard.GetProperty("rulesetVersionId").GetGuid()
            .Should().Be(rulesetVersionId);
        createdCard.GetProperty("bouts").EnumerateArray().Should().OnlyContain(item =>
            item.GetProperty("rulesetVersionId").GetGuid() == rulesetVersionId);
        var boutIds = createdCard.GetProperty("bouts")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        createdCard.GetProperty("bouts")[7]
            .GetProperty("scheduledRounds").GetInt32().Should().Be(5);
        createdCard.GetProperty("bouts")[7]
            .GetProperty("cardSection").GetString().Should().Be("MainEvent");
        var adminCard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/combat/card");
        adminCard.GetProperty("canReorder").GetBoolean().Should().BeTrue();
        adminCard.GetProperty("rulesetVersionId").GetGuid()
            .Should().Be(rulesetVersionId);
        adminCard.GetProperty("allowedResultMethods").GetArrayLength().Should().Be(3);
        var consoleBout = adminCard.GetProperty("bouts")[0];
        consoleBout.GetProperty("fighters").GetArrayLength().Should().Be(2);
        consoleBout.GetProperty("fighters")[0].GetProperty("id").GetGuid()
            .Should().Be(fighters[0].Id);
        consoleBout.GetProperty("officials").GetArrayLength().Should().Be(4);
        consoleBout.GetProperty("rounds").GetArrayLength().Should().Be(3);
        consoleBout.GetProperty("rounds")[0].GetProperty("status").GetString()
            .Should().Be("Pending");

        var invalidOrder = boutIds.Reverse().ToArray();
        var invalidOrderResponse = await client.PutAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/card/order",
            new { boutIds = invalidOrder, minutesBetweenBouts = 30 });
        invalidOrderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reorderedIds = boutIds.Take(4).Reverse()
            .Concat(boutIds.Skip(4)).ToArray();
        var reorderResponse = await client.PutAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/card/order",
            new { boutIds = reorderedIds, minutesBetweenBouts = 20 });
        reorderResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await reorderResponse.Content.ReadAsStringAsync());
        var reorderedCard = await reorderResponse.Content.ReadFromJsonAsync<JsonElement>();
        reorderedCard.GetProperty("bouts")[0].GetProperty("id").GetGuid()
            .Should().Be(boutIds[3]);
        reorderedCard.GetProperty("bouts")[0].GetProperty("sortOrder").GetInt32()
            .Should().Be(1);

        (await client.PostAsync(
            $"/api/competitions/{competitionId}/publish",
            null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/card/order",
            new { boutIds, minutesBetweenBouts = 30 })).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/combat/card"))
            .GetProperty("canReorder").GetBoolean().Should().BeFalse();
        (await client.PostAsync(
            $"/api/competitions/{competitionId}/combat/bouts/{boutIds[0]}/rounds/1/start",
            null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var disabledMethodResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/bouts/{boutIds[0]}/result",
            new
            {
                winnerId = fighters[0].Id,
                resultMethod = "Technical Knockout",
                round = 1,
                timeInRoundSeconds = 30,
                scorecards = Array.Empty<object>()
            });
        disabledMethodResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await disabledMethodResponse.Content.ReadAsStringAsync())
            .Should().Contain("not enabled");
        (await client.PostAsync(
            $"/api/competitions/{competitionId}/combat/bouts/{boutIds[0]}/rounds/1/end",
            null)).StatusCode.Should().Be(HttpStatusCode.OK);

        for (var index = 0; index < boutIds.Length; index++)
        {
            var rounds = index == 7 ? 5 : 3;
            var method = (index % 3) switch
            {
                0 => "Unanimous Decision",
                1 => "Knockout",
                _ => "Submission"
            };
            var winnerId = index % 3 == 2
                ? fighters[index * 2 + 1].Id
                : fighters[index * 2].Id;
            var scorecards = method == "Unanimous Decision"
                ? officialSets[index].Skip(1).Select(official => new
                {
                    officialId = official.officialId,
                    rounds = Enumerable.Range(1, rounds).Select(round => new
                    {
                        roundNumber = round,
                        fighter1Score = 10,
                        fighter2Score = 9
                    }).ToArray()
                }).ToArray()
                : Array.Empty<object>();
            var resultResponse = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/combat/bouts/{boutIds[index]}/result",
                new
                {
                    winnerId,
                    resultMethod = method,
                    round = method == "Unanimous Decision" ? (int?)null : 2,
                    timeInRoundSeconds = method == "Unanimous Decision"
                        ? (int?)null
                        : 145,
                    scorecards,
                    notes = "Official integration result"
                });
            resultResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await resultResponse.Content.ReadAsStringAsync());
        }

        var correctionResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/bouts/{boutIds[0]}/result/corrections",
            new
            {
                result = new
                {
                    winnerId = fighters[1].Id,
                    resultMethod = "Knockout",
                    round = 2,
                    timeInRoundSeconds = 88,
                    scorecards = Array.Empty<object>(),
                    notes = "Corrected official result"
                },
                reason = "The corners were transposed on the original decision."
            });
        correctionResponse.StatusCode.Should().Be(
            HttpStatusCode.Accepted,
            await correctionResponse.Content.ReadAsStringAsync());
        var correction = await correctionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var correctionId = correction.GetProperty("correctionId").GetGuid();

        var pendingCard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/combat/card");
        var pendingBout = pendingCard.GetProperty("bouts").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == boutIds[0]);
        pendingBout.GetProperty("resultMethod").GetString()
            .Should().Be("Unanimous Decision");
        pendingBout.GetProperty("pendingCorrections")
            .GetArrayLength().Should().Be(1);

        var approveCorrection = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/combat/bouts/{boutIds[0]}/result/corrections/{correctionId}/approve",
            new { reason = "Reviewed against the signed result sheet." });
        approveCorrection.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await approveCorrection.Content.ReadAsStringAsync());
        var correctedResult = await approveCorrection.Content.ReadFromJsonAsync<JsonElement>();
        correctedResult.GetProperty("resultMethod").GetString().Should().Be("Knockout");
        correctedResult.GetProperty("winnerId").GetGuid().Should().Be(fighters[1].Id);

        var publicCard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/ufc/combat/card?competitionId={competitionId}");
        publicCard.GetProperty("boutCount").GetInt32().Should().Be(8);
        publicCard.GetProperty("completedBoutCount").GetInt32().Should().Be(8);
        publicCard.GetProperty("allowedResultMethods").EnumerateArray()
            .Select(item => item.GetString()).Should().Equal(
                "Unanimous Decision", "Knockout", "Submission");
        var publicBouts = publicCard.GetProperty("bouts")
            .EnumerateArray()
            .ToArray();
        publicBouts[0].GetProperty("id").GetGuid().Should().Be(boutIds[3]);
        publicBouts.Should().OnlyContain(bout =>
            bout.GetProperty("officials").GetArrayLength() == 4);
        publicBouts.Select(bout => bout.GetProperty("resultMethod").GetString())
            .Should().Contain(new[]
            {
                "Unanimous Decision",
                "Knockout",
                "Submission"
            });
        publicBouts[0].GetProperty("scorecards").GetArrayLength().Should().Be(3);
        publicBouts[7].GetProperty("cardSection").GetString()
            .Should().Be("MainEvent");
        publicBouts[7].GetProperty("scheduledRounds").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task AthleticsMeet_QualifiesTwoHeatsAndPublishesFinalMedals()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-athletics-meet");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var rulesetResponse = await client.PostAsJsonAsync(
            "/api/sports/athletics/configure",
            new { createdBy = "api-integration-athletics-meet" });
        rulesetResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await rulesetResponse.Content.ReadAsStringAsync());
        var configuredRuleset = await rulesetResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var athleticsRulesetVersionId = configuredRuleset.GetProperty("id").GetGuid();
        var currentRuleset = await client.GetFromJsonAsync<JsonElement>(
            "/api/sports/athletics/ruleset");
        currentRuleset.GetProperty("id").GetGuid().Should().Be(athleticsRulesetVersionId);

        var tenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000004");
        var sportId =
            Guid.Parse("00000000-0000-0000-0000-000000000103");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var competitionResponse = await client.PostAsJsonAsync(
            "/api/competitions",
            new
            {
                name = $"Fresh Athletics Meet {suffix}",
                sportId,
                tenantId
            });
        competitionResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        var athleteIds = new List<Guid>();
        for (var index = 0; index < 16; index++)
        {
            var athleteResponse = await client.PostAsJsonAsync(
                "/api/participants/athletes",
                new
                {
                    name = $"Sprinter {suffix} {index + 1:D2}",
                    shortName = $"S{index + 1:D2}{suffix[..2]}",
                    tenantId
                });
            athleteResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await athleteResponse.Content.ReadAsStringAsync());
            var athlete = await athleteResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var participantId = athlete.GetProperty("id").GetGuid();
            athleteIds.Add(participantId);
            var registrationResponse = await client.PostAsJsonAsync(
                $"/api/registrations/competition/{competitionId}",
                new { participantId, type = 0 });
            registrationResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registration = await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var registrationId = registration.GetProperty("id").GetGuid();
            (await client.PostAsync(
                $"/api/registrations/{registrationId}/submit",
                null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsJsonAsync(
                $"/api/registrations/{registrationId}/approve",
                new { reviewerId = Guid.NewGuid(), notes = "Eligible sprinter" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var meetResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/athletics/meet",
            new
            {
                eventName = "Men's 100m",
                editionName = "2026",
                stageName = "Track",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                venue = "Integration Track",
                distanceMeters = 100,
                expectedParticipants = 16,
                heatCount = 2,
                lanesPerHeat = 8,
                qualifyByPlace = 3,
                qualifyByTime = 2,
                advanceTies = true,
                minutesBetweenHeats = 15,
                semifinalCount = 2,
                semifinalQualifyByPlace = 2,
                semifinalQualifyByTime = 2,
                laneAssignment = "Seeded",
                advancePlaceTies = false,
                advanceTimeTies = true
            });
        meetResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await meetResponse.Content.ReadAsStringAsync());
        var meet = await meetResponse.Content.ReadFromJsonAsync<JsonElement>();
        meet.GetProperty("heatCount").GetInt32().Should().Be(2);
        meet.GetProperty("rulesetVersionId").GetGuid()
            .Should().Be(athleticsRulesetVersionId);
        var heats = meet.GetProperty("heats").EnumerateArray().ToArray();
        heats.Should().HaveCount(2);
        heats.Should().OnlyContain(heat =>
            heat.GetProperty("lanes").GetArrayLength() == 8);
        foreach (var heat in heats)
        {
            heat.GetProperty("lanes").EnumerateArray()
                .Select(lane => lane.GetProperty("lane").GetInt32())
                .Should().Equal(Enumerable.Range(1, 8));
        }

        var fieldResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/athletics/field-events",
            new
            {
                eventName = "Women's Long Jump",
                metricType = "Distance",
                editionName = "2026 Field",
                stageName = "Field Events",
                startTimeUtc = DateTime.UtcNow.AddDays(1).AddHours(2),
                participantIds = athleteIds.Take(4).ToArray(),
                venue = "Integration Field",
                maxAttempts = 3,
                recordThresholdMeters = 7.5m
            });
        fieldResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await fieldResponse.Content.ReadAsStringAsync());
        var fieldEvent = await fieldResponse.Content.ReadFromJsonAsync<JsonElement>();
        fieldEvent.GetProperty("rulesetVersionId").GetGuid()
            .Should().Be(athleticsRulesetVersionId);
        var fieldEventId = fieldEvent.GetProperty("fieldEventId").GetGuid();

        var fieldResultsResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/athletics/field-events/{fieldEventId}/results",
            new
            {
                results = new object[]
                {
                    new { participantId = athleteIds[0], attempts = new[] { new { measurementMeters = (decimal?)7.40m }, new { measurementMeters = (decimal?)7.62m }, new { measurementMeters = (decimal?)null } } },
                    new { participantId = athleteIds[1], attempts = new[] { new { measurementMeters = (decimal?)7.20m }, new { measurementMeters = (decimal?)7.55m }, new { measurementMeters = (decimal?)7.50m } } },
                    new { participantId = athleteIds[2], attempts = new[] { new { measurementMeters = (decimal?)7.30m }, new { measurementMeters = (decimal?)7.40m }, new { measurementMeters = (decimal?)7.45m } } },
                    new { participantId = athleteIds[3], attempts = Array.Empty<object>(), disqualified = true, disqualificationReason = "Footwear violation" }
                }
            });
        fieldResultsResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await fieldResultsResponse.Content.ReadAsStringAsync());
        var fieldResults = await fieldResultsResponse.Content.ReadFromJsonAsync<JsonElement>();
        fieldResults.GetProperty("rankingDirection").GetString().Should().Be("HigherIsBetter");
        fieldResults.GetProperty("rankings")[0].GetProperty("participantId").GetGuid()
            .Should().Be(athleteIds[0]);
        fieldResults.GetProperty("rankings")[0].GetProperty("medal").GetString()
            .Should().Be("Gold");
        fieldResults.GetProperty("rankings")[0].GetProperty("isRecord").GetBoolean()
            .Should().BeTrue();
        fieldResults.GetProperty("disqualifications").GetArrayLength().Should().Be(1);
        var adminMeetResponse = await client.GetAsync(
            $"/api/competitions/{competitionId}/athletics/meet");
        adminMeetResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await adminMeetResponse.Content.ReadAsStringAsync());
        var adminMeet = await adminMeetResponse.Content.ReadFromJsonAsync<JsonElement>();
        adminMeet.GetProperty("races").GetArrayLength().Should().Be(2);
        var adminFieldEvent = adminMeet.GetProperty("fieldEvents")[0];
        adminFieldEvent.GetProperty("metricType").GetString().Should().Be("Distance");
        adminFieldEvent.GetProperty("participants").EnumerateArray()
            .Single(participant => participant.GetProperty("id").GetGuid() == athleteIds[0])
            .GetProperty("isRecord").GetBoolean().Should().BeTrue();
        (await client.PostAsync(
            $"/api/competitions/{competitionId}/publish",
            null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var heatTimes = new[]
        {
            new[] { 10_000, 10_200, 10_400, 10_550, 10_700, 10_900, 11_100, 11_300 },
            new[] { 10_100, 10_300, 10_500, 10_600, 10_800, 11_000, 11_200, 11_400 }
        };
        var disqualifiedId = heats[0].GetProperty("lanes")[3]
            .GetProperty("participantId").GetGuid();
        for (var heatIndex = 0; heatIndex < heats.Length; heatIndex++)
        {
            var lanes = heats[heatIndex].GetProperty("lanes")
                .EnumerateArray()
                .ToArray();
            var results = lanes.Select((lane, laneIndex) => (object)new
            {
                participantId = lane.GetProperty("participantId").GetGuid(),
                timeMilliseconds = heatIndex == 0 && laneIndex == 3
                    ? (int?)null
                    : heatTimes[heatIndex][laneIndex],
                disqualified = heatIndex == 0 && laneIndex == 3,
                disqualificationReason = heatIndex == 0 && laneIndex == 3
                    ? "False start"
                    : null
            }).ToArray();
            var resultResponse = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/athletics/races/"
                + $"{heats[heatIndex].GetProperty("id").GetGuid()}/results",
                new { results });
            resultResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await resultResponse.Content.ReadAsStringAsync());
        }

        var qualificationResponse = await client.PostAsync(
            $"/api/competitions/{competitionId}/athletics/qualify",
            null);
        qualificationResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await qualificationResponse.Content.ReadAsStringAsync());
        var qualification = await qualificationResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        qualification.GetProperty("nextRound").GetString().Should().Be("Semifinal");
        qualification.GetProperty("finalContestId").ValueKind.Should().Be(JsonValueKind.Null);
        qualification.GetProperty("nextRoundContestIds").GetArrayLength().Should().Be(2);
        qualification.GetProperty("requestedSlots").GetInt32().Should().Be(8);
        qualification.GetProperty("qualifiedCount").GetInt32().Should().Be(8);
        var qualifiedAdminMeet = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/athletics/meet");
        qualifiedAdminMeet.GetProperty("races").EnumerateArray()
            .Where(race => race.GetProperty("contestShape").GetString() == "Heat")
            .SelectMany(race => race.GetProperty("participants").EnumerateArray())
            .Should().Contain(participant =>
                participant.GetProperty("qualificationStatus").GetString() == "Qualified");
        var qualifiers = qualification.GetProperty("qualifiers")
            .EnumerateArray()
            .ToArray();
        qualifiers.Count(item => item.GetProperty("qualificationReason")
                .GetString()!.Contains("by place"))
            .Should().Be(6);
        qualifiers.Count(item => item.GetProperty("qualificationReason")
                .GetString()!.Contains("by time"))
            .Should().Be(2);
        qualifiers.Select(item => item.GetProperty("participantId").GetGuid())
            .Should().NotContain(disqualifiedId);
        qualification.GetProperty("eliminated").EnumerateArray()
            .Single(item => item.GetProperty("participantId").GetGuid() == disqualifiedId)
            .GetProperty("qualificationReason").GetString().Should().Be("False start");

        var semifinalMeet = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/world-athletics/athletics/meet"
            + $"?competitionId={competitionId}");
        var semifinals = semifinalMeet.GetProperty("semifinals")
            .EnumerateArray().ToArray();
        semifinals.Should().HaveCount(2);
        semifinals.Should().OnlyContain(semifinal =>
            semifinal.GetProperty("entries").GetArrayLength() == 4);
        semifinals.Should().OnlyContain(semifinal =>
            semifinal.GetProperty("qualifyByPlace").GetInt32() == 2
            && semifinal.GetProperty("qualifyByTime").GetInt32() == 2
            && !semifinal.GetProperty("advancePlaceTies").GetBoolean()
            && semifinal.GetProperty("advanceTimeTies").GetBoolean()
            && semifinal.GetProperty("laneAssignment").GetString() == "Seeded");
        foreach (var semifinal in semifinals)
        {
            semifinal.GetProperty("entries").EnumerateArray()
                .Select(entry => entry.GetProperty("lane").GetInt32())
                .Should().BeEquivalentTo(new[] { 3, 4, 5, 6 });
            var results = semifinal.GetProperty("entries").EnumerateArray()
                .OrderBy(entry => entry.GetProperty("lane").GetInt32())
                .Select((entry, index) => new
                {
                    participantId = entry.GetProperty("participantId").GetGuid(),
                    timeMilliseconds = 10_000
                        + (semifinal.GetProperty("raceNumber").GetInt32() * 10)
                        + (index * 100),
                    disqualified = false
                })
                .ToArray();
            var semifinalResult = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/athletics/races/"
                + $"{semifinal.GetProperty("id").GetGuid()}/results",
                new { results });
            semifinalResult.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await semifinalResult.Content.ReadAsStringAsync());
        }

        var finalQualificationResponse = await client.PostAsync(
            $"/api/competitions/{competitionId}/athletics/qualify",
            null);
        finalQualificationResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await finalQualificationResponse.Content.ReadAsStringAsync());
        var finalQualification = await finalQualificationResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        finalQualification.GetProperty("nextRound").GetString().Should().Be("Final");
        finalQualification.GetProperty("requestedSlots").GetInt32().Should().Be(6);
        finalQualification.GetProperty("qualifiedCount").GetInt32().Should().Be(6);
        var finalContestId = finalQualification.GetProperty("finalContestId").GetGuid();
        var orderedQualifiers = finalQualification.GetProperty("qualifiers")
            .EnumerateArray()
            .OrderBy(item => item.GetProperty("timeMilliseconds").GetDouble())
            .ToArray();
        var finalTimes = new[]
        {
            9_800, 9_900, 10_000, 10_100, 10_100, 10_300
        };
        var finalResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/athletics/races/"
            + $"{finalContestId}/results",
            new
            {
                results = orderedQualifiers.Select((qualifier, index) => new
                {
                    participantId = qualifier.GetProperty("participantId").GetGuid(),
                    timeMilliseconds = finalTimes[index],
                    disqualified = false
                })
            });
        finalResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await finalResponse.Content.ReadAsStringAsync());

        var publicMeet = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/world-athletics/athletics/meet"
            + $"?competitionId={competitionId}");
        publicMeet.GetProperty("heats").GetArrayLength().Should().Be(2);
        publicMeet.GetProperty("semifinals").GetArrayLength().Should().Be(2);
        var publicHeats = publicMeet.GetProperty("heats")
            .EnumerateArray()
            .ToArray();
        publicHeats.SelectMany(heat => heat.GetProperty("entries").EnumerateArray())
            .Single(entry => entry.GetProperty("participantId").GetGuid()
                == disqualifiedId)
            .GetProperty("status").GetString().Should().Be("Disqualified");
        var finalEntries = publicMeet.GetProperty("final")
            .GetProperty("entries")
            .EnumerateArray()
            .ToArray();
        finalEntries.Should().HaveCount(6);
        finalEntries[0].GetProperty("medal").GetString().Should().Be("Gold");
        finalEntries[1].GetProperty("medal").GetString().Should().Be("Silver");
        finalEntries[2].GetProperty("medal").GetString().Should().Be("Bronze");
        finalEntries.Where(entry => entry.GetProperty("isTied").GetBoolean())
            .Should().HaveCount(2);
        finalEntries.Where(entry => entry.GetProperty("isTied").GetBoolean())
            .Should().OnlyContain(entry =>
                entry.GetProperty("place").GetInt32() == 4);
    }

    [Fact]
    public async Task T20Tournament_RecordsOversAdvancesKnockoutAndPublishesChampion()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-cricket-t20");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var tenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000005");
        var sportId =
            Guid.Parse("00000000-0000-0000-0000-000000000104");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var rulesetResponse = await client.PostAsJsonAsync(
            "/api/sports/cricket/configure",
            new { createdBy = "api-integration-cricket-t20" });
        rulesetResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await rulesetResponse.Content.ReadAsStringAsync());
        var configuredRuleset = await rulesetResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var rulesetVersionId = configuredRuleset.GetProperty("id").GetGuid();
        var currentRuleset = await client.GetFromJsonAsync<JsonElement>(
            "/api/sports/cricket/ruleset");
        currentRuleset.GetProperty("id").GetGuid().Should().Be(rulesetVersionId);

        var competitionResponse = await client.PostAsJsonAsync(
            "/api/competitions",
            new
            {
                name = $"Fresh T20 Cup {suffix}",
                sportId,
                tenantId
            });
        competitionResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await competitionResponse.Content.ReadAsStringAsync());
        var competition = await competitionResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        for (var index = 1; index <= 4; index++)
        {
            var teamResponse = await client.PostAsJsonAsync(
                "/api/participants/teams",
                new
                {
                    name = $"Cricket {suffix} Team {index}",
                    shortName = $"C{index}{suffix[..3]}",
                    tenantId
                });
            teamResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await teamResponse.Content.ReadAsStringAsync());
            var team = await teamResponse.Content.ReadFromJsonAsync<JsonElement>();
            var registrationResponse = await client.PostAsJsonAsync(
                $"/api/registrations/competition/{competitionId}",
                new
                {
                    participantId = team.GetProperty("id").GetGuid(),
                    type = 1
                });
            registrationResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registration = await registrationResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            var registrationId = registration.GetProperty("id").GetGuid();
            (await client.PostAsync(
                $"/api/registrations/{registrationId}/submit",
                null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsJsonAsync(
                $"/api/registrations/{registrationId}/approve",
                new { reviewerId = Guid.NewGuid(), notes = "Eligible T20 team" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var tournamentResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/cricket/tournament",
            new
            {
                editionName = "2026",
                groupStageName = "Group stage",
                venue = "Integration Cricket Ground",
                startTimeUtc = DateTime.UtcNow.AddDays(1)
            });
        tournamentResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await tournamentResponse.Content.ReadAsStringAsync());
        var tournament = await tournamentResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        var groupMatches = tournament.GetProperty("matches")
            .EnumerateArray()
            .ToArray();
        groupMatches.Should().HaveCount(6);
        tournament.GetProperty("rulesetVersionId").GetGuid()
            .Should().Be(rulesetVersionId);
        groupMatches.Should().OnlyContain(match =>
            match.GetProperty("rulesetVersionId").GetGuid() == rulesetVersionId);

        var invalidMatch = groupMatches[0];
        var invalidResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/cricket/matches/"
            + $"{invalidMatch.GetProperty("id").GetGuid()}/innings",
            new
            {
                inningsNumber = 1,
                battingTeamId = invalidMatch.GetProperty("participantIds")[0].GetGuid(),
                deliveries = new[]
                {
                    new { overNumber = 1, ballNumber = 2, runs = 0 }
                }
            });
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var incrementalMatchId = invalidMatch.GetProperty("id").GetGuid();
        var incrementalTeams = invalidMatch.GetProperty("participantIds")
            .EnumerateArray().Select(item => item.GetGuid()).ToArray();
        JsonElement incrementalScorecard = default;
        for (var ball = 0; ball < 10; ball++)
        {
            var ballResponse = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/cricket/matches/"
                + $"{incrementalMatchId}/deliveries",
                new
                {
                    inningsNumber = 1,
                    battingTeamId = incrementalTeams[0],
                    runs = 1,
                    extraRuns = 0,
                    isWicket = true
                });
            ballResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await ballResponse.Content.ReadAsStringAsync());
            incrementalScorecard = await ballResponse.Content
                .ReadFromJsonAsync<JsonElement>();
        }
        incrementalScorecard.GetProperty("innings")[0]
            .GetProperty("runs").GetInt32().Should().Be(10);
        incrementalScorecard.GetProperty("innings")[0]
            .GetProperty("overs").GetString().Should().Be("1.4");
        incrementalScorecard.GetProperty("innings")[0]
            .GetProperty("overSummaries").GetArrayLength().Should().Be(2);

        var firstComplete = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/cricket/matches/"
            + $"{incrementalMatchId}/innings/1/complete",
            new { battingTeamId = incrementalTeams[0] });
        firstComplete.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await firstComplete.Content.ReadAsStringAsync());
        var completedFirst = await firstComplete.Content.ReadFromJsonAsync<JsonElement>();
        completedFirst.GetProperty("target").GetInt32().Should().Be(11);

        var firstDeliveryId = completedFirst.GetProperty("innings")[0]
            .GetProperty("deliveries")[0].GetProperty("eventId").GetGuid();
        var correctionResponse = await client.PutAsJsonAsync(
            $"/api/competitions/{competitionId}/cricket/matches/"
            + $"{incrementalMatchId}/deliveries/{firstDeliveryId}",
            new
            {
                runs = 2,
                extraRuns = 0,
                isWicket = true,
                reason = "Boundary signal corrected by the official"
            });
        correctionResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await correctionResponse.Content.ReadAsStringAsync());
        var corrected = await correctionResponse.Content.ReadFromJsonAsync<JsonElement>();
        corrected.GetProperty("innings")[0].GetProperty("runs").GetInt32()
            .Should().Be(11);
        corrected.GetProperty("target").GetInt32().Should().Be(12);

        for (var ball = 0; ball < 2; ball++)
        {
            var chaseBall = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/cricket/matches/"
                + $"{incrementalMatchId}/deliveries",
                new
                {
                    inningsNumber = 2,
                    battingTeamId = incrementalTeams[1],
                    runs = 6,
                    extraRuns = 0,
                    isWicket = false
                });
            chaseBall.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await chaseBall.Content.ReadAsStringAsync());
            incrementalScorecard = await chaseBall.Content
                .ReadFromJsonAsync<JsonElement>();
            if (ball == 0)
                incrementalScorecard.GetProperty("runsRequired").GetInt32().Should().Be(6);
        }
        var chaseComplete = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/cricket/matches/"
            + $"{incrementalMatchId}/innings/2/complete",
            new { battingTeamId = incrementalTeams[1] });
        chaseComplete.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await chaseComplete.Content.ReadAsStringAsync());
        var incrementalResult = await chaseComplete.Content.ReadFromJsonAsync<JsonElement>();
        incrementalResult.GetProperty("result").GetString()
            .Should().Be("Won by 10 wickets");

        static object[] Innings(int winningRuns = 120) =>
            Enumerable.Range(0, 120)
                .Select(index => (object)new
                {
                    overNumber = (index / 6) + 1,
                    ballNumber = (index % 6) + 1,
                    runs = index == 119 ? winningRuns - 119 : 1,
                    extraRuns = 0,
                    isWicket = false
                })
                .ToArray();

        async Task CompleteMatch(JsonElement match)
        {
            var matchId = match.GetProperty("id").GetGuid();
            var teams = match.GetProperty("participantIds")
                .EnumerateArray()
                .Select(item => item.GetGuid())
                .ToArray();
            var firstResponse = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/cricket/matches/{matchId}/innings",
                new
                {
                    inningsNumber = 1,
                    battingTeamId = teams[0],
                    deliveries = Innings()
                });
            firstResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await firstResponse.Content.ReadAsStringAsync());
            var chaseResponse = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/cricket/matches/{matchId}/innings",
                new
                {
                    inningsNumber = 2,
                    battingTeamId = teams[1],
                    deliveries = Innings(121)
                });
            chaseResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await chaseResponse.Content.ReadAsStringAsync());
            var chase = await chaseResponse.Content.ReadFromJsonAsync<JsonElement>();
            chase.GetProperty("target").GetInt32().Should().Be(121);
            chase.GetProperty("runs").GetInt32().Should().Be(121);
            chase.GetProperty("result").GetString().Should().Be("Won by 10 wickets");
        }

        foreach (var match in groupMatches.Skip(1))
            await CompleteMatch(match);

        var qualificationResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/cricket/qualify",
            new
            {
                stageName = "Knockout",
                venue = "Integration Cricket Ground",
                startTimeUtc = DateTime.UtcNow.AddDays(8),
                daysUntilFinal = 2
            });
        qualificationResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await qualificationResponse.Content.ReadAsStringAsync());
        var qualification = await qualificationResponse.Content
            .ReadFromJsonAsync<JsonElement>();
        qualification.GetProperty("standings").GetArrayLength().Should().Be(4);
        var semifinals = qualification.GetProperty("semifinals")
            .EnumerateArray()
            .ToArray();
        semifinals.Should().HaveCount(2);
        semifinals.Should().OnlyContain(match =>
            match.GetProperty("rulesetVersionId").GetGuid() == rulesetVersionId);
        qualification.GetProperty("final").GetProperty("rulesetVersionId")
            .GetGuid().Should().Be(rulesetVersionId);
        foreach (var semifinal in semifinals)
            await CompleteMatch(semifinal);

        var knockoutState = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/cricket/tournament");
        var final = knockoutState.GetProperty("final");
        final.GetProperty("teams").GetArrayLength().Should().Be(2);
        var finalAsMatch = JsonSerializer.SerializeToElement(new
        {
            id = final.GetProperty("id").GetGuid(),
            participantIds = final.GetProperty("teams")
                .EnumerateArray()
                .Select(item => item.GetProperty("participantId").GetGuid())
                .ToArray()
        });
        await CompleteMatch(finalAsMatch);

        var publishCricket = await client.PostAsync(
            $"/api/competitions/{competitionId}/publish",
            null);
        publishCricket.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            await publishCricket.Content.ReadAsStringAsync());
        var publicTournament = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/cricket-world/cricket/tournament"
            + $"?competitionId={competitionId}");
        publicTournament.GetProperty("groupMatches").GetArrayLength().Should().Be(6);
        publicTournament.GetProperty("semifinals").GetArrayLength().Should().Be(2);
        publicTournament.GetProperty("standings")[0]
            .GetProperty("played").GetInt32().Should().Be(3);
        publicTournament.GetProperty("final")
            .GetProperty("status").GetString().Should().Be("Completed");
        publicTournament.GetProperty("championName").GetString()
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Admin_CanRejectApprovePublishAndRebuildPublicStandings()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "api-integration-result-lifecycle-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        var contestsResponse = await client.GetAsync(
            $"/api/contests?tenantId={scorer.TenantId}");
        using var contests = JsonDocument.Parse(
            await contestsResponse.Content.ReadAsStringAsync());
        var contest = contests.RootElement.EnumerateArray().First(item =>
            item.GetProperty("participants").GetArrayLength() > 0
            && item.GetProperty("segments").GetArrayLength() > 0);
        var contestId = contest.GetProperty("id").GetGuid();
        var participantId = contest.GetProperty("participants")[0]
            .GetProperty("participantId").GetGuid();
        var segmentId = contest.GetProperty("segments")[0]
            .GetProperty("id").GetGuid();

        var score = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId = Guid.NewGuid(),
                participantId,
                metricName = "score",
                value = 1,
                scoredBy = "result-lifecycle-test",
                segmentId,
                clockSeconds = 20m
            });
        score.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await score.Content.ReadAsStringAsync());
        var submission = await client.PostAsync(
            $"/api/results/contest/{contestId}/submit",
            null);
        using var submitted = JsonDocument.Parse(
            await submission.Content.ReadAsStringAsync());
        var resultId = submitted.RootElement.GetProperty("results")[0]
            .GetProperty("id").GetGuid();

        var scorerReview = await client.PostAsync(
            $"/api/results/{resultId}/review", null);
        scorerReview.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-result-lifecycle-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var review = await client.PostAsync($"/api/results/{resultId}/review", null);
        review.StatusCode.Should().Be(HttpStatusCode.OK,
            await review.Content.ReadAsStringAsync());
        using (var reviewPayload = JsonDocument.Parse(await review.Content.ReadAsStringAsync()))
        {
            reviewPayload.RootElement.GetProperty("status").GetInt32()
                .Should().Be((int)ContestResultStatus.UnderReview);
        }

        var rejected = await client.PostAsJsonAsync(
            $"/api/results/{resultId}/reject",
            new { reason = "Verify scorer notes" });
        rejected.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await rejected.Content.ReadAsStringAsync());
        using (var rejectedPayload = JsonDocument.Parse(
                   await rejected.Content.ReadAsStringAsync()))
        {
            rejectedPayload.RootElement.GetProperty("remarks").GetString()
                .Should().Be("Verify scorer notes");
        }

        var resubmitted = await client.PostAsync(
            $"/api/results/contest/{contestId}/submit",
            null);
        resubmitted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/results/{resultId}/review", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await client.PostAsJsonAsync(
            $"/api/results/{resultId}/approve",
            new { approvedBy = Guid.NewGuid() });
        approved.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await approved.Content.ReadAsStringAsync());

        var beforePublication = await client.GetStringAsync(
            $"/api/public/tenants/premier-league/results?contestId={contestId}");
        beforePublication.Should().NotContain(resultId.ToString());

        var published = await client.PostAsync(
            $"/api/results/{resultId}/publish",
            null);
        published.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await published.Content.ReadAsStringAsync());

        var publicResults = await client.GetStringAsync(
            $"/api/public/tenants/premier-league/results?contestId={contestId}");
        publicResults.Should().Contain(resultId.ToString());
        var publicStandings = await client.GetStringAsync(
            "/api/public/tenants/premier-league/standings");
        publicStandings.Should().Contain(participantId.ToString());

        var missingReason = await client.PostAsJsonAsync(
            $"/api/results/{resultId}/reopen", new { reason = "" });
        missingReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reopened = await client.PostAsJsonAsync(
            $"/api/results/{resultId}/reopen",
            new { reason = "Official appeal accepted" });
        reopened.StatusCode.Should().Be(HttpStatusCode.OK,
            await reopened.Content.ReadAsStringAsync());
        using (var reopenedPayload = JsonDocument.Parse(await reopened.Content.ReadAsStringAsync()))
        {
            reopenedPayload.RootElement.GetProperty("resultStatus").GetInt32()
                .Should().Be((int)ContestResultStatus.Corrected);
        }

        var corrected = await client.PostAsJsonAsync(
            $"/api/results/{resultId}/correct",
            new
            {
                score = 2,
                scoreDisplay = "2 score",
                rank = 1,
                position = "1",
                reason = "Appeal correction"
            });
        corrected.StatusCode.Should().Be(HttpStatusCode.OK,
            await corrected.Content.ReadAsStringAsync());
        (await client.PostAsync($"/api/results/contest/{contestId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/results/{resultId}/review", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(
            $"/api/results/{resultId}/approve", new { approvedBy = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var auditScope = _factory.Services.CreateScope();
        var database = auditScope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var reopenAudit = await database.AuditEntries
            .Where(item => item.EntityType == nameof(ContestResult)
                && item.EntityId == resultId.ToString()
                && item.Action == "ResultReopenedForCorrection")
            .OrderByDescending(item => item.Timestamp)
            .FirstAsync();
        reopenAudit.Reason.Should().Be("Official appeal accepted");
        reopenAudit.UserId.Should().NotBeNull();
        reopenAudit.OriginalValue.Should().Contain("Published");
        reopenAudit.NewValue.Should().Contain("Corrected");
    }

    [Fact]
    public async Task Rugby_SupportsRulesScoringBonusPointsAndPublicStandings()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-rugby");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var configured = await client.PostAsJsonAsync(
            "/api/sports/rugby/configure",
            new { createdBy = "rugby-acceptance-test" });
        configured.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await configured.Content.ReadAsStringAsync());
        (await client.GetAsync("/api/sports/rugby/ruleset"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var rugbyTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000008");
        var competitionId =
            Guid.Parse("00000000-0000-0000-0000-000000000306");
        var contests = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/contests?tenantId={rugbyTenantId}");
        var contest = contests!.First(item =>
            item.GetProperty("participants").GetArrayLength() == 2
            && item.GetProperty("segments").GetArrayLength() > 0);
        var contestId = contest.GetProperty("id").GetGuid();
        var segmentId = contest.GetProperty("segments")[0]
            .GetProperty("id").GetGuid();
        var homeId = contest.GetProperty("participants")[0]
            .GetProperty("participantId").GetGuid();
        var awayId = contest.GetProperty("participants")[1]
            .GetProperty("participantId").GetGuid();
        var publicTournament = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/six-nations/rugby/tournament?competitionId={competitionId}");
        publicTournament.GetProperty("matches").GetArrayLength().Should().Be(15);
        publicTournament.GetProperty("matches").EnumerateArray().Should().OnlyContain(
            match => match.GetProperty("status").GetString() == "Scheduled");

        var schemaResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/schema");
        schemaResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await schemaResponse.Content.ReadAsStringAsync());
        using (var schema = JsonDocument.Parse(
                   await schemaResponse.Content.ReadAsStringAsync()))
        {
            schema.RootElement.GetProperty("sportCode").GetString()
                .Should().Be("rugby");
            var actions = schema.RootElement.GetProperty("actions")
                .EnumerateArray().ToArray();
            actions.Single(item => item.GetProperty("code").GetString() == "try")
                .GetProperty("defaultValue").GetDecimal().Should().Be(5);
            actions.Single(item => item.GetProperty("code").GetString() == "penalty-try")
                .GetProperty("defaultValue").GetDecimal().Should().Be(7);
        }

        async Task AddScores(Guid participantId, decimal value, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var response = await client.PostAsJsonAsync(
                    $"/api/livescoring/contests/{contestId}/events",
                    new
                    {
                        eventId = Guid.NewGuid(),
                        participantId,
                        metricName = "score",
                        value,
                        scoredBy = "rugby-acceptance-test",
                        segmentId,
                        clockSeconds = 60m + index
                    });
                response.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await response.Content.ReadAsStringAsync());
            }
        }

        await AddScores(homeId, 5, 4);
        await AddScores(homeId, 2, 4);
        await AddScores(awayId, 5, 3);
        await AddScores(awayId, 2, 3);

        var submittedResponse = await client.PostAsync(
            $"/api/results/contest/{contestId}/submit",
            null);
        submittedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await submittedResponse.Content.ReadAsStringAsync());
        using var submitted = JsonDocument.Parse(
            await submittedResponse.Content.ReadAsStringAsync());
        foreach (var result in submitted.RootElement.GetProperty("results").EnumerateArray())
        {
            var approved = await client.PostAsJsonAsync(
                $"/api/results/{result.GetProperty("id").GetGuid()}/approve",
                new { approvedBy = Guid.NewGuid() });
            approved.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await approved.Content.ReadAsStringAsync());
        }

        var standings = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/public/tenants/six-nations/standings?competitionId={competitionId}");
        var home = standings!.Single(item =>
            item.GetProperty("participantId").GetGuid() == homeId);
        var away = standings!.Single(item =>
            item.GetProperty("participantId").GetGuid() == awayId);
        home.GetProperty("pointsFor").GetInt32().Should().Be(28);
        home.GetProperty("pointsAgainst").GetInt32().Should().Be(21);
        home.GetProperty("points").GetDecimal().Should().Be(5);
        away.GetProperty("pointsFor").GetInt32().Should().Be(21);
        away.GetProperty("pointsAgainst").GetInt32().Should().Be(28);
        away.GetProperty("points").GetDecimal().Should().Be(1);
        home.GetProperty("metadata").GetString().Should().Contain("tryBonusPoints");
        away.GetProperty("metadata").GetString().Should().Contain("losingBonusPoints");
    }

    [Fact]
    public async Task RugbySevens_HasDistinctRulesTimingDemoAndPoolStandings()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-rugby-sevens");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var configured = await client.PostAsJsonAsync(
            "/api/sports/rugby/configure",
            new { createdBy = "rugby-sevens-acceptance", variant = "7s" });
        configured.StatusCode.Should().Be(HttpStatusCode.OK,
            await configured.Content.ReadAsStringAsync());
        using var configuredJson = JsonDocument.Parse(
            await configured.Content.ReadAsStringAsync());
        using var validation = JsonDocument.Parse(configuredJson.RootElement
            .GetProperty("validationConfiguration").GetString()!);
        validation.RootElement.GetProperty("variant").GetString().Should().Be("sevens");
        validation.RootElement.GetProperty("playersPerSide").GetInt32().Should().Be(7);
        validation.RootElement.GetProperty("timing").GetProperty("halfDurationMinutes")
            .GetInt32().Should().Be(7);
        validation.RootElement.GetProperty("timing").GetProperty("sinBinMinutes")
            .GetInt32().Should().Be(2);

        var rulesetResponse = await client.GetAsync(
            "/api/sports/rugby/ruleset?variant=sevens");
        rulesetResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await rulesetResponse.Content.ReadAsStringAsync());
        var badVariant = await client.GetAsync(
            "/api/sports/rugby/ruleset?variant=league");
        badVariant.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tournament = await client.GetFromJsonAsync<JsonElement>(
            "/api/public/tenants/national-sports-demo/rugby/tournament" +
            "?competitionId=70000000-0000-0000-0000-000000000100");
        tournament.GetProperty("variant").GetString().Should().Be("sevens");
        tournament.GetProperty("formatName").GetString().Should().Be("Rugby Sevens");
        tournament.GetProperty("playersPerSide").GetInt32().Should().Be(7);
        tournament.GetProperty("halfDurationMinutes").GetInt32().Should().Be(7);
        tournament.GetProperty("standingsModel").GetProperty("winPoints")
            .GetInt32().Should().Be(3);
        tournament.GetProperty("standingsModel").GetProperty("lossPoints")
            .GetInt32().Should().Be(1);
        tournament.GetProperty("standingsModel").GetProperty("usesTryBonus")
            .GetBoolean().Should().BeFalse();
        tournament.GetProperty("matches").GetArrayLength().Should().Be(15);
        tournament.GetProperty("standings").GetArrayLength().Should().Be(8);
        tournament.GetProperty("matches").EnumerateArray().Should().OnlyContain(
            match => match.GetProperty("status").GetString() == "Approved");
        tournament.GetProperty("standings").EnumerateArray().Should().OnlyContain(
            row => row.GetProperty("metadata").GetString()!.Contains(
                "\"variant\":\"sevens\"", StringComparison.Ordinal));

        var firstMatchId = tournament.GetProperty("matches")[0]
            .GetProperty("id").GetGuid();
        var scoringSchema = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{firstMatchId}/schema");
        scoringSchema.GetProperty("name").GetString().Should().Be("Rugby Sevens");
        scoringSchema.GetProperty("actions").GetArrayLength().Should().Be(8);
    }

    [Fact]
    public async Task Administrator_CanUpdateBrandingExposedToPublicAndMobileClients()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-mobile-branding");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var premierTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");

        var update = await client.PutAsJsonAsync(
            $"/api/branding/tenant/{premierTenantId}",
            new
            {
                tenantName = "Premier League Mobile",
                shortName = "PLM",
                logoUrl = "https://cdn.example.test/premier-mobile.svg",
                primaryColor = "#0F766E",
                secondaryColor = "#7C3AED",
                accentColor = "#F59E0B"
            });
        update.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await update.Content.ReadAsStringAsync());

        var publicTenant = await client.GetFromJsonAsync<JsonElement>(
            "/api/public/tenants/premier-league");
        publicTenant.GetProperty("branding").GetProperty("primaryColor")
            .GetString().Should().Be("#0F766E");
        publicTenant.GetProperty("branding").GetProperty("logoUrl")
            .GetString().Should().Be("https://cdn.example.test/premier-mobile.svg");
    }

    [Fact]
    public async Task SeededTenantAdministrator_IsRestrictedToPremierLeagueResources()
    {
        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-exact-tenant-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var premierTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId =
            Guid.Parse("00000000-0000-0000-0000-000000000003");

        var tenants = await client.GetFromJsonAsync<JsonElement[]>("/api/tenants");
        tenants.Should().ContainSingle();
        tenants![0].GetProperty("id").GetGuid().Should().Be(premierTenantId);
        (await client.GetAsync($"/api/competitions?tenantId={premierTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/competitions?tenantId={nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/branding/tenant/{nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownBranding = await client.PutAsJsonAsync(
            $"/api/branding/tenant/{premierTenantId}",
            new
            {
                tenantName = "Premier Tenant Admin",
                shortName = "PTA",
                logoUrl = (string?)null,
                primaryColor = "#3D195B",
                secondaryColor = "#00FF85",
                accentColor = "#04F5FF"
            });
        ownBranding.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await ownBranding.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync(
            "/api/tenants",
            new { name = "Forbidden", shortName = "NO", slug = $"no-{Guid.NewGuid():N}" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reports_AreQueuedGeneratedDownloadedAndTenantScoped()
    {
        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-reports");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var premierTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var competitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");

        var create = await client.PostAsJsonAsync("/api/reports", new
        {
            tenantId = premierTenantId,
            name = "Premier fixture schedule",
            type = "FixtureSchedule",
            format = "Csv",
            competitionId,
            fromUtc = (DateTime?)null,
            toUtc = (DateTime?)null
        });
        create.StatusCode.Should().Be(HttpStatusCode.Accepted,
            await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var reportId = created.GetProperty("id").GetGuid();

        JsonElement report = default;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            report = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{reportId}");
            if (report.GetProperty("status").GetString() is "Completed" or "Failed") break;
            await Task.Delay(100);
        }
        report.GetProperty("status").GetString().Should().Be(
            "Completed", report.TryGetProperty("errorMessage", out var error) ? error.GetString() : null);

        var download = await client.GetAsync($"/api/reports/{reportId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        download.Content.Headers.ContentDisposition!.FileNameStar
            .Should().Be("Premier fixture schedule.csv");
        var csv = await download.Content.ReadAsStringAsync();
        csv.Should().Contain("Competition,Contest,Start (UTC),Venue,Status,Participants");
        csv.Should().Contain("Premier League");

        var excelCreate = await client.PostAsJsonAsync("/api/reports", new
        {
            tenantId = premierTenantId,
            name = "Premier results",
            type = "Results",
            format = "Excel",
            competitionId
        });
        excelCreate.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var excelCreated = await excelCreate.Content.ReadFromJsonAsync<JsonElement>();
        var excelId = excelCreated.GetProperty("id").GetGuid();
        JsonElement excelReport = default;
        for (var attempt = 0; attempt < 150; attempt++)
        {
            excelReport = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{excelId}");
            if (excelReport.GetProperty("status").GetString() is "Completed" or "Failed") break;
            await Task.Delay(200);
        }
        excelReport.GetProperty("status").GetString().Should().Be(
            "Completed",
            excelReport.TryGetProperty("errorMessage", out var excelError)
                ? excelError.GetString()
                : null);
        var excelDownload = await client.GetAsync($"/api/reports/{excelId}/download");
        excelDownload.StatusCode.Should().Be(HttpStatusCode.OK);
        excelDownload.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
          var workbookBytes = await excelDownload.Content.ReadAsByteArrayAsync();
          workbookBytes.Take(2).Should().Equal((byte)'P', (byte)'K');

          var pdfCreate = await client.PostAsJsonAsync("/api/reports", new
          {
              tenantId = premierTenantId,
              name = "Premier match sheets PDF",
              type = "MatchSheet",
              format = "Pdf",
              competitionId
          });
          pdfCreate.StatusCode.Should().Be(HttpStatusCode.Accepted);
          var pdfCreated = await pdfCreate.Content.ReadFromJsonAsync<JsonElement>();
          var pdfId = pdfCreated.GetProperty("id").GetGuid();
          JsonElement pdfReport = default;
          for (var attempt = 0; attempt < 150; attempt++)
          {
              pdfReport = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{pdfId}");
              if (pdfReport.GetProperty("status").GetString() is "Completed" or "Failed") break;
              await Task.Delay(200);
          }
          pdfReport.GetProperty("status").GetString().Should().Be(
              "Completed",
              pdfReport.TryGetProperty("errorMessage", out var pdfError)
                  ? pdfError.GetString()
                  : null);
          var pdfDownload = await client.GetAsync($"/api/reports/{pdfId}/download");
          pdfDownload.StatusCode.Should().Be(HttpStatusCode.OK);
          pdfDownload.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
          pdfDownload.Content.Headers.ContentDisposition!.FileNameStar
              .Should().Be("Premier match sheets PDF.pdf");
          var pdfBytes = await pdfDownload.Content.ReadAsByteArrayAsync();
          pdfBytes.Length.Should().BeGreaterThan(1000);
          pdfBytes.Take(5).Should().Equal(
              (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-');
          using (var pdfStream = new MemoryStream(pdfBytes))
          using (var pdf = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import))
          {
              pdf.PageCount.Should().BeGreaterThan(0);
              pdf.Info.Title.Should().Be("Match sheets");
          }

          var ownList = await client.GetFromJsonAsync<JsonElement>(
            $"/api/reports?tenantId={premierTenantId}");
        ownList.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetGuid() == reportId);
        (await client.GetAsync($"/api/reports?tenantId={nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/reports", new
        {
            tenantId = nbaTenantId,
            name = "Forbidden NBA export",
            type = "Results",
            format = "Excel"
          })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
      }

    [Fact]
    public async Task ContestSheets_AreSportAwareBrandedSponsoredArchivedAndTenantScoped()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var contestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var rugbySport = await database.Sports.AsNoTracking()
                .FirstAsync(item => item.Code.ToLower().Contains("rugby"));
            var firstTeam = new Team(Guid.NewGuid(), tenantId, "Archive Lions", "Lions");
            var secondTeam = new Team(Guid.NewGuid(), tenantId, "Archive Rhinos", "Rhinos");
            var contest = new Contest(contestId, tenantId, rugbySport.Id, "TeamVsTeam");
            contest.SetName("Sponsored Rugby Final");
            contest.AddParticipant(firstTeam.Id, "Team", 1);
            contest.AddParticipant(secondTeam.Id, "Team", 2);
            contest.AddSegment("First Half", "Half", 1);
            contest.AddSegment("Second Half", "Half", 2);
            contest.AddOfficial(Guid.NewGuid(), "Referee", "Morgan Referee");
            contest.Schedule("Archive Stadium", DateTime.UtcNow.AddDays(1), 80);
            var sponsor = new Sponsor(Guid.NewGuid(), tenantId, "Archive Telecom");
            sponsor.SetLogoUrl("https://assets.example/archive-telecom.svg");
            var sponsorship = new Sponsorship(
                Guid.NewGuid(), tenantId, sponsor.Id, SponsorshipType.Title,
                "Rugby final title partner", 5000m);
            sponsorship.SetContest(contestId);
            sponsorship.SetPlacement(SponsorPlacements.ContestSheet, 0);
            sponsorship.Approve();
            database.Participants.AddRange(firstTeam, secondTeam);
            database.Contests.Add(contest);
            database.Sponsors.Add(sponsor);
            database.Sponsorships.Add(sponsorship);
            await database.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "api-contest-sheets");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var branding = await client.PutAsJsonAsync($"/api/branding/tenant/{tenantId}", new
        {
            tenantName = "Premier Archive Brand",
            shortName = "PAB",
            logoUrl = "https://assets.example/pab.svg",
            primaryColor = "#3D195B",
            secondaryColor = "#00FF85",
            accentColor = "#04F5FF"
        });
        branding.StatusCode.Should().Be(HttpStatusCode.OK,
            await branding.Content.ReadAsStringAsync());

        var create = await client.PostAsJsonAsync("/api/contest-sheets", new
        {
            tenantId,
            contestId,
            name = "Sponsored rugby final archive"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Accepted,
            await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("template").GetString().Should().Be("Rugby match sheet");
        created.GetProperty("sportCode").GetString().Should().BeEquivalentTo("rugby");
        var sheetId = created.GetProperty("id").GetGuid();

        JsonElement sheet = default;
        for (var attempt = 0; attempt < 150; attempt++)
        {
            sheet = await client.GetFromJsonAsync<JsonElement>(
                $"/api/contest-sheets/{sheetId}");
            if (sheet.GetProperty("status").GetString() is "Completed" or "Failed") break;
            await Task.Delay(200);
        }
        sheet.GetProperty("status").GetString().Should().Be(
            "Completed",
            sheet.TryGetProperty("errorMessage", out var error) ? error.GetString() : null);
        var archivedList = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contest-sheets?tenantId={tenantId}&contestId={contestId}");
        archivedList.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("id").GetGuid() == sheetId);

        var download = await client.GetAsync($"/api/contest-sheets/{sheetId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        download.Content.Headers.ContentDisposition!.FileNameStar
            .Should().Be("Sponsored rugby final archive.pdf");
        var bytes = await download.Content.ReadAsByteArrayAsync();
        bytes.Take(5).Should().Equal(
            (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-');
        using (var stream = new MemoryStream(bytes))
        using (var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import))
        {
            pdf.PageCount.Should().BeGreaterThan(0);
            pdf.Info.Title.Should().Be("Rugby match sheet: Sponsored Rugby Final");
            pdf.Info.Author.Should().Be("Premier Archive Brand");
            pdf.Info.Subject.Should().Contain("Archive Telecom");
        }

        (await client.GetAsync($"/api/contest-sheets?tenantId={nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var foreignContest = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/contests?tenantId={tenantId}");
        foreignContest.Should().Contain(item => item.GetProperty("id").GetGuid() == contestId);
    }

    [Fact]
    public async Task TenantDataExport_IsCompleteCredentialSafeTenantIsolatedAndAudited()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var exportName = $"Premier complete export {Guid.NewGuid():N}";
        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "api-tenant-export");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);

        var create = await client.PostAsJsonAsync("/api/tenant-exports", new
        {
            tenantId,
            name = exportName
        });
        create.StatusCode.Should().Be(HttpStatusCode.Accepted,
            await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var exportId = created.GetProperty("id").GetGuid();
        JsonElement status = default;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            status = await client.GetFromJsonAsync<JsonElement>(
                $"/api/tenant-exports/{exportId}");
            if (status.GetProperty("status").GetString() is "Completed" or "Failed") break;
            await Task.Delay(200);
        }
        status.GetProperty("status").GetString().Should().Be(
            "Completed",
            status.TryGetProperty("errorMessage", out var error) ? error.GetString() : null);

        var download = await client.GetAsync($"/api/tenant-exports/{exportId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        download.Content.Headers.ContentDisposition!.FileNameStar
            .Should().Be($"{exportName}.zip");
        var bytes = await download.Content.ReadAsByteArrayAsync();
        bytes.Take(2).Should().Equal((byte)'P', (byte)'K');
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Select(item => item.FullName).Should().Contain(new[]
        {
            "manifest.json", "tenant.json", "branding.json", "users.json",
            "sports.json", "participants.json", "competitions.json", "contests.json",
            "results.json", "registrations.json", "standings.json", "live-scores.json",
            "notifications.json", "officials.json", "brackets.json",
            "leaderboards.json", "subscriptions.json", "report-archives.json",
            "sponsors.json", "sponsorships.json",
            "audit-entries.json"
        });
        var manifest = JsonDocument.Parse(await ReadEntryAsync("manifest.json"));
        manifest.RootElement.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        manifest.RootElement.GetProperty("schema").GetString()
            .Should().Be("sports-platform-tenant-export/v1");
        var tenantJson = await ReadEntryAsync("tenant.json");
        tenantJson.Should().Contain("Premier League");
        tenantJson.Should().NotContain("National Basketball Association");
        var usersJson = await ReadEntryAsync("users.json");
        usersJson.Should().Contain("admin@premier.com");
        usersJson.Should().NotContain("scorer@nba.com");
        usersJson.Should().NotContain("PasswordHash");
        usersJson.Should().NotContain("RefreshToken");
        (await ReadEntryAsync("participants.json")).Should().NotContain("NBA Hidden");

        (await client.GetAsync($"/api/tenant-exports?tenantId={nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/tenant-exports", new
        {
            tenantId = nbaTenantId,
            name = "Forbidden NBA export"
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var actions = await database.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.EntityId == exportId.ToString())
            .Select(item => item.Action)
            .ToArrayAsync();
        actions.Should().Contain(new[]
        {
            "TenantDataExportRequested",
            "TenantDataExportCompleted",
            "TenantDataExportDownloaded"
        });

        async Task<string> ReadEntryAsync(string name)
        {
            var entry = archive.GetEntry(name);
            entry.Should().NotBeNull();
            await using var entryStream = entry!.Open();
            using var reader = new StreamReader(
                entryStream, Encoding.UTF8, true, 1024, leaveOpen: false);
            return await reader.ReadToEndAsync();
        }
    }

    [Fact]
    public async Task RegistrationAndRosterReports_ContainTenantCompetitionData()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var competitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");
        var athleteId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var athlete = new Athlete(
                athleteId, tenantId, "Reporting Athlete", "Report Athlete");
            var team = new Team(teamId, tenantId, "Reporting United", "Reporting");
            team.AddRoster("Competition squad", competitionId)
                .AddAthlete(athleteId, "17", "Wing");
            var registration = Registration.CreateForCompetition(
                Guid.NewGuid(), competitionId, athleteId, RegistrationType.Individual);
            registration.Submit();
            registration.Approve(Guid.NewGuid(), "Eligible for reporting");
            database.Participants.AddRange(athlete, team);
            database.Registrations.Add(registration);
            await database.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-registration-roster-reports");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);

        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/reports/catalog");
        catalog.GetProperty("reportTypes").EnumerateArray().Should().Contain(item =>
            item.GetProperty("value").GetString() == "RegistrationList"
            && item.GetProperty("available").GetBoolean());
        catalog.GetProperty("reportTypes").EnumerateArray().Should().Contain(item =>
            item.GetProperty("value").GetString() == "TeamRoster"
            && item.GetProperty("available").GetBoolean());

        var registrationDownload = await GenerateAsync(
            "RegistrationList", "Csv", "Premier registration list");
        registrationDownload.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var registrationCsv = await registrationDownload.Content.ReadAsStringAsync();
        registrationCsv.Should().Contain(
            "Competition,Contest,Participant,Participant type,Registration type,Status");
        registrationCsv.Should().Contain("Reporting Athlete");
        registrationCsv.Should().Contain("Approved");
        registrationCsv.Should().Contain("Eligible for reporting");

        var rosterDownload = await GenerateAsync(
            "TeamRoster", "Html", "Premier team roster");
        rosterDownload.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var rosterHtml = await rosterDownload.Content.ReadAsStringAsync();
        rosterHtml.Should().Contain("Reporting United");
        rosterHtml.Should().Contain("Competition squad");
        rosterHtml.Should().Contain("Reporting Athlete");
        rosterHtml.Should().Contain("17");
        rosterHtml.Should().Contain("Wing");

        async Task<HttpResponseMessage> GenerateAsync(
            string type,
            string format,
            string name)
        {
            var create = await client.PostAsJsonAsync("/api/reports", new
            {
                tenantId,
                competitionId,
                name,
                type,
                format
            });
            create.StatusCode.Should().Be(
                HttpStatusCode.Accepted, await create.Content.ReadAsStringAsync());
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            var reportId = created.GetProperty("id").GetGuid();
            JsonElement report = default;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                report = await client.GetFromJsonAsync<JsonElement>(
                    $"/api/reports/{reportId}");
                if (report.GetProperty("status").GetString() is "Completed" or "Failed")
                    break;
                await Task.Delay(100);
            }
            report.GetProperty("status").GetString().Should().Be(
                "Completed",
                report.TryGetProperty("errorMessage", out var error)
                    ? error.GetString()
                    : null);
            var download = await client.GetAsync($"/api/reports/{reportId}/download");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            return download;
        }
    }

    [Fact]
    public async Task OfficialAssignmentAndMatchSheetReports_UseCurrentContestData()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var competitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var firstTeamId = Guid.NewGuid();
        var secondTeamId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var stageId = await database.Competitions.AsNoTracking()
                .Where(item => item.Id == competitionId && item.TenantId == tenantId)
                .SelectMany(item => item.Editions)
                .SelectMany(item => item.Stages)
                .Select(item => item.Id)
                .FirstAsync();
            var firstTeam = new Team(
                firstTeamId, tenantId, "Report Lions", "Lions");
            var secondTeam = new Team(
                secondTeamId, tenantId, "Report Leopards", "Leopards");
            var contest = new Contest(contestId, tenantId, sportId, "TeamVsTeam");
            contest.SetName("Report Cup Final");
            contest.AssignToStage(stageId, 99);
            contest.AddParticipant(firstTeamId, "Team", 1);
            contest.AddParticipant(secondTeamId, "Team", 2);
            contest.AddSegment("First Half", "Half", 1);
            contest.AddSegment("Second Half", "Half", 2);
            contest.AddOfficial(Guid.NewGuid(), "Referee", "Referee Rowan");
            contest.AddOfficial(Guid.NewGuid(), "Assistant", "Assistant Avery");
            contest.Schedule("Reporting Stadium", DateTime.UtcNow.AddDays(2), 90);
            database.Participants.AddRange(firstTeam, secondTeam);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-official-match-reports");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);

        var officialDownload = await GenerateAsync(
            "OfficialAssignment", "Csv", "Premier official assignments");
        var officialCsv = await officialDownload.Content.ReadAsStringAsync();
        officialCsv.Should().Contain(
            "Competition,Sport,Contest,Start (UTC),Venue,Official,Role,Confirmation,Contest status");
        officialCsv.Should().Contain("Report Cup Final");
        officialCsv.Should().Contain("Referee Rowan");
        officialCsv.Should().Contain("Assistant Avery");
        officialCsv.Should().Contain("Reporting Stadium");
        officialCsv.Should().NotContain("NBA Hidden Official");

        var matchDownload = await GenerateAsync(
            "MatchSheet", "Html", "Premier match sheets");
        var matchHtml = await matchDownload.Content.ReadAsStringAsync();
        matchHtml.Should().Contain("Report Cup Final");
        matchHtml.Should().Contain("Report Lions");
        matchHtml.Should().Contain("Report Leopards");
        matchHtml.Should().Contain("First Half | Second Half");
        matchHtml.Should().Contain("Referee: Referee Rowan");
        matchHtml.Should().Contain("Reporting Stadium");
        matchHtml.Should().NotContain("NBA Hidden Contest");

        async Task<HttpResponseMessage> GenerateAsync(
            string type,
            string format,
            string name)
        {
            var create = await client.PostAsJsonAsync("/api/reports", new
            {
                tenantId,
                competitionId,
                name,
                type,
                format
            });
            create.StatusCode.Should().Be(
                HttpStatusCode.Accepted, await create.Content.ReadAsStringAsync());
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            var reportId = created.GetProperty("id").GetGuid();
            JsonElement report = default;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                report = await client.GetFromJsonAsync<JsonElement>(
                    $"/api/reports/{reportId}");
                if (report.GetProperty("status").GetString() is "Completed" or "Failed")
                    break;
                await Task.Delay(100);
            }
            report.GetProperty("status").GetString().Should().Be(
                "Completed",
                report.TryGetProperty("errorMessage", out var error)
                    ? error.GetString()
                    : null);
            var download = await client.GetAsync($"/api/reports/{reportId}/download");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            return download;
        }
    }

    [Fact]
    public async Task StandingsAndCompetitionSummaryReports_UsePersistedCompetitionState()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var competitionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var competition = new Competition(
                competitionId, tenantId, "Reporting Summary Cup", sportId);
            var edition = competition.AddEdition(
                "2026", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
            var stage = edition.AddStage("Reporting Group Stage", StageType.Group, 1);
            var group = stage.AddGroup("Reporting Group A");
            competition.Publish();
            var participant = new Team(
                participantId, tenantId, "Reporting Table Leaders", "Leaders");
            var registration = Registration.CreateForCompetition(
                Guid.NewGuid(), competitionId, participantId, RegistrationType.Team);
            registration.Submit();
            registration.Approve(Guid.NewGuid(), "Accepted into summary cup");
            var contest = new Contest(
                Guid.NewGuid(), tenantId, sportId, "TeamVsTeam");
            contest.SetName("Reporting Summary Fixture");
            contest.AssignToStage(stage.Id, 1);
            contest.AddParticipant(participantId, "Team", 1);
            contest.Schedule("Summary Ground", DateTime.UtcNow.AddDays(1), 90);
            var standing = new Standing(
                Guid.NewGuid(), competitionId, group.Id, participantId);
            standing.UpdateStats(3, 2, 1, 0, 8, 3, 9);
            standing.SetPosition(1);
            standing.SetMetadata("{\"bonusPoints\":2}");
            database.Competitions.Add(competition);
            database.Participants.Add(participant);
            database.Registrations.Add(registration);
            database.Contests.Add(contest);
            database.Standings.Add(standing);
            await database.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-standings-summary-reports");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);

        var standingsDownload = await GenerateAsync(
            "Standings", "Csv", "Reporting cup standings");
        var standingsCsv = await standingsDownload.Content.ReadAsStringAsync();
        standingsCsv.Should().Contain(
            "Competition,Group,Position,Participant,Played,Won,Drawn,Lost,Points for,Points against,Differential,Points,Additional metrics,Updated (UTC)");
        standingsCsv.Should().Contain("Reporting Summary Cup");
        standingsCsv.Should().Contain("Reporting Group A");
        standingsCsv.Should().Contain("Reporting Table Leaders");
        standingsCsv.Should().Contain("bonusPoints");
        standingsCsv.Should().NotContain("NBA Hidden Standing");

        var summaryDownload = await GenerateAsync(
            "CompetitionSummary", "Html", "Reporting competition summary");
        var summaryHtml = await summaryDownload.Content.ReadAsStringAsync();
        summaryHtml.Should().Contain("Reporting Summary Cup");
        summaryHtml.Should().Contain("Football");
        summaryHtml.Should().Contain("Published");
        summaryHtml.Should().Contain("Distinct participants");
        summaryHtml.Should().NotContain("NBA Hidden Competition");

        async Task<HttpResponseMessage> GenerateAsync(
            string type,
            string format,
            string name)
        {
            var create = await client.PostAsJsonAsync("/api/reports", new
            {
                tenantId,
                competitionId,
                name,
                type,
                format
            });
            create.StatusCode.Should().Be(
                HttpStatusCode.Accepted, await create.Content.ReadAsStringAsync());
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            var reportId = created.GetProperty("id").GetGuid();
            JsonElement report = default;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                report = await client.GetFromJsonAsync<JsonElement>(
                    $"/api/reports/{reportId}");
                if (report.GetProperty("status").GetString() is "Completed" or "Failed")
                    break;
                await Task.Delay(100);
            }
            report.GetProperty("status").GetString().Should().Be(
                "Completed",
                report.TryGetProperty("errorMessage", out var error)
                    ? error.GetString()
                    : null);
            var download = await client.GetAsync($"/api/reports/{reportId}/download");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            return download;
        }
    }

    [Fact]
    public async Task DisciplineAthletePerformanceAndAuditReports_UseAuthoritativeData()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000100");
        var competitionId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var actorId = await database.Users.Where(item => item.TenantId == tenantId)
                .Select(item => item.Id).FirstAsync();
            var competition = new Competition(
                competitionId, tenantId, "Reporting Performance Meet", sportId);
            var edition = competition.AddEdition(
                "2026", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(2));
            var stage = edition.AddStage("Performance Final", StageType.Heat, 1);
            competition.Publish();
            var athlete = new Athlete(
                athleteId, tenantId, "Reporting Sprint Star", "Sprint Star");
            var contest = new Contest(contestId, tenantId, sportId, "Race");
            contest.SetName("Reporting Performance Final");
            contest.AssignToStage(stage.Id, 1);
            contest.AddParticipant(athleteId, "Athlete", 1);
            var segmentId = contest.AddSegment("Final", "Race", 1).Id;
            contest.Schedule("Performance Arena", DateTime.UtcNow.AddDays(1), 15);
            var result = new ContestResult(
                Guid.NewGuid(), contestId, Guid.NewGuid(), athleteId);
            result.SetScore(10.42m, "10.42 s", "00:00:10.420");
            result.SetRank(1, "Gold");
            result.SetPerformanceData(
                "{\"reactionTime\":0.142}", "{\"wind\":1.1}");
            result.MarkAsPersonalBest();
            result.MarkAsSeasonBest();
            result.MarkAsCompetitionRecord();
            result.SetRemarks("Championship performance");
            var card = new LiveScore(
                Guid.NewGuid(), contestId, Guid.NewGuid(), athleteId,
                "yellow_cards", 1, segmentId, 120, tenantId,
                "YELLOW_CARD", deviceId: "report-test",
                idempotencyKey: $"report-card-{contestId:N}");
            card.SetDisplayValue("Yellow card");
            card.SetMetadata("{\"reason\":\"Unsporting conduct\"}");
            var disqualification = new ContestCheckInHistory(
                Guid.NewGuid(), tenantId, contestId, "ParticipantDisqualified",
                "Athlete", athleteId, "Entered", "Disqualified", actorId,
                "Equipment violation");
            var audit = new AuditEntry(
                Guid.NewGuid(), tenantId, actorId, "PerformanceReviewed", "Contest",
                contestId.ToString(), "{\"status\":\"Pending\"}",
                "{\"status\":\"Reviewed\"}", "Result documentation checked");
            audit.SetRequestContext("127.0.0.1", "report-integration-test");
            var hiddenAudit = new AuditEntry(
                Guid.NewGuid(), nbaTenantId, null, "HiddenNBAAction", "Contest",
                Guid.NewGuid().ToString(), null, "{\"marker\":\"NBA private audit\"}", null);
            database.Competitions.Add(competition);
            database.Participants.Add(athlete);
            database.Contests.Add(contest);
            database.ContestResults.Add(result);
            database.LiveScores.Add(card);
            database.ContestCheckInHistory.Add(disqualification);
            database.AuditEntries.AddRange(audit, hiddenAudit);
            await database.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-final-report-families");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);

        var disciplineDownload = await GenerateAsync(
            "Discipline", "Csv", "Reporting discipline");
        var disciplineCsv = await disciplineDownload.Content.ReadAsStringAsync();
        disciplineCsv.Should().Contain("Reporting Performance Final");
        disciplineCsv.Should().Contain("Reporting Sprint Star");
        disciplineCsv.Should().Contain("YELLOW_CARD");
        disciplineCsv.Should().Contain("Unsporting conduct");
        disciplineCsv.Should().Contain("Equipment violation");

        var performanceDownload = await GenerateAsync(
            "AthletePerformance", "Html", "Reporting athlete performance");
        var performanceHtml = await performanceDownload.Content.ReadAsStringAsync();
        performanceHtml.Should().Contain("Reporting Sprint Star");
        performanceHtml.Should().Contain("10.42 s");
        performanceHtml.Should().Contain("reactionTime");
        performanceHtml.Should().Contain("Championship performance");
        performanceHtml.Should().Contain("Competition record");

        var auditDownload = await GenerateAsync(
            "AuditHistory", "Csv", "Reporting audit history");
        var auditCsv = await auditDownload.Content.ReadAsStringAsync();
        auditCsv.Should().Contain("PerformanceReviewed");
        auditCsv.Should().Contain("Result documentation checked");
        auditCsv.Should().Contain("report-integration-test");
        auditCsv.Should().NotContain("NBA private audit");

        async Task<HttpResponseMessage> GenerateAsync(
            string type,
            string format,
            string name)
        {
            var create = await client.PostAsJsonAsync("/api/reports", new
            {
                tenantId,
                competitionId,
                name,
                type,
                format
            });
            create.StatusCode.Should().Be(
                HttpStatusCode.Accepted, await create.Content.ReadAsStringAsync());
            var created = await create.Content.ReadFromJsonAsync<JsonElement>();
            var reportId = created.GetProperty("id").GetGuid();
            JsonElement report = default;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                report = await client.GetFromJsonAsync<JsonElement>(
                    $"/api/reports/{reportId}");
                if (report.GetProperty("status").GetString() is "Completed" or "Failed")
                    break;
                await Task.Delay(100);
            }
            report.GetProperty("status").GetString().Should().Be(
                "Completed",
                report.TryGetProperty("errorMessage", out var error)
                    ? error.GetString()
                    : null);
            var download = await client.GetAsync($"/api/reports/{reportId}/download");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            return download;
        }
    }

      [Fact]
      public async Task SubscriptionEntitlementsAndUsageLimits_AreTenantScopedAndEnforcedOnWrites()
    {
        using var isolatedFactory = new ApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-entitlement-enforcement");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var active = await client.GetFromJsonAsync<JsonElement>(
            $"/api/subscriptions/active/{tenantId}");
        var subscriptionId = active.GetProperty("id").GetGuid();
        var entitlements = await client.GetFromJsonAsync<JsonElement>(
            $"/api/subscriptions/entitlements/{tenantId}");
        entitlements.GetProperty("sports.max_competitions").GetBoolean().Should().BeTrue();

        var usage = await client.GetFromJsonAsync<JsonElement>(
            $"/api/subscriptions/usage/{tenantId}");
        var currentUsage = usage.GetProperty("current");
        currentUsage.TryGetProperty("max_admins", out _).Should().BeTrue();
        currentUsage.TryGetProperty("max_operators", out _).Should().BeTrue();
        currentUsage.TryGetProperty("storage_mb", out _).Should().BeTrue();
        currentUsage.TryGetProperty("notifications_per_month", out _).Should().BeTrue();
        currentUsage.GetProperty("api_requests_per_hour").GetInt32().Should().BeGreaterThan(0);
        usage.GetProperty("hardLimitsEnabled").GetBoolean().Should().BeTrue();
        var currentCompetitions = usage.GetProperty("current")
            .GetProperty("max_competitions").GetInt32();
        var setLimits = await client.PutAsJsonAsync(
            $"/api/subscriptions/{subscriptionId}/limits",
            new Dictionary<string, int> { ["max_competitions"] = currentCompetitions });
        setLimits.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var warningSummary = await client.GetFromJsonAsync<JsonElement>(
            $"/api/subscriptions/usage/{tenantId}");
        warningSummary.GetProperty("warnings").EnumerateArray()
            .Should().Contain(warning =>
                warning.GetProperty("metric").GetString() == "max_competitions" &&
                warning.GetProperty("severity").GetString() == "exceeded");

        var atLimit = await client.PostAsJsonAsync("/api/competitions", new
        {
            name = "Must be blocked by the plan limit",
            sportId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            tenantId
        });
        atLimit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await atLimit.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("error").GetString().Should().Be("UsageLimitExceeded");

        var setEntitlements = await client.PutAsJsonAsync(
            $"/api/subscriptions/{subscriptionId}/entitlements",
            new Dictionary<string, bool>
            {
                ["branding.custom_colors"] = false,
                ["usage.hard_limits"] = false,
            });
        setEntitlements.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var softLimitCreate = await client.PostAsJsonAsync("/api/competitions", new
        {
            name = "Allowed while the plan uses soft limits",
            sportId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            tenantId
        });
        softLimitCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var softLimitSummary = await client.GetFromJsonAsync<JsonElement>(
            $"/api/subscriptions/usage/{tenantId}");
        softLimitSummary.GetProperty("hardLimitsEnabled").GetBoolean().Should().BeFalse();
        softLimitSummary.GetProperty("warnings").EnumerateArray()
            .Single(warning => warning.GetProperty("metric").GetString() == "max_competitions")
            .GetProperty("hardLimitEnforced").GetBoolean().Should().BeFalse();
        var forbiddenBranding = await client.PutAsJsonAsync(
            $"/api/branding/tenant/{tenantId}",
            new
            {
                tenantName = "Premier League",
                shortName = "PL",
                logoUrl = (string?)null,
                primaryColor = "#123456",
                secondaryColor = "#654321",
                accentColor = "#ABCDEF"
            });
        forbiddenBranding.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forbiddenBranding.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("error").GetString().Should().Be("FeatureNotEntitled");
    }

    [Fact]
    public async Task KnockoutPlayoffs_SupportTwoLegTiesThirdPlaceAndApprovedOverrideHistory()
    {
        using var isolatedFactory = new ApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "knockout-options-requester");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var competitionId = Guid.NewGuid();
        var participantIds = Enumerable.Range(0, 4)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{200 + index:D12}"))
            .ToArray();

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var competition = new Competition(
                competitionId,
                tenantId,
                "Aggregate Cup",
                Guid.Parse("00000000-0000-0000-0000-000000000100"));
            competition.AddEdition("2026", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
            database.Competitions.Add(competition);
            foreach (var (participantId, index) in participantIds.Select((id, index) => (id, index)))
            {
                var standing = new Standing(Guid.NewGuid(), competitionId, Guid.Empty, participantId);
                standing.UpdateStats(3, 3 - index, 0, index, 10 - index, index, 12 - index);
                standing.SetPosition(index + 1);
                database.Standings.Add(standing);
            }
            await database.SaveChangesAsync();
        }

        var generatedResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/top-four",
            new
            {
                bracketName = "Aggregate Cup Finals",
                stageName = "Knockout",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                venue = "National Stadium",
                daysUntilFinal = 14,
                legsPerTie = 2,
                daysBetweenLegs = 7,
                includeThirdPlace = true
            });
        generatedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await generatedResponse.Content.ReadAsStringAsync());
        var generated = await generatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        generated.GetProperty("semiFinalContestIds").GetArrayLength().Should().Be(4);
        generated.GetProperty("finalContestIds").GetArrayLength().Should().Be(2);
        generated.GetProperty("thirdPlaceContestId").GetGuid().Should().NotBeEmpty();

        var bracket = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/playoffs");
        var matches = bracket.GetProperty("rounds").EnumerateArray()
            .SelectMany(round => round.GetProperty("matches").EnumerateArray()).ToArray();
        matches.Count(match => match.GetProperty("isAggregateTie").GetBoolean()).Should().Be(3);
        matches.Should().ContainSingle(match => match.GetProperty("isThirdPlace").GetBoolean());
        var semifinal = matches.First(match =>
            match.GetProperty("loserNextMatchId").ValueKind != JsonValueKind.Null);
        var matchId = semifinal.GetProperty("id").GetGuid();
        var requestedWinner = semifinal.GetProperty("participant1Id").GetGuid();

        var requestedResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/matches/{matchId}/overrides",
            new { winnerId = requestedWinner, reason = "Aggregate tie resolved on penalties" });
        requestedResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var requested = await requestedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var overrideId = requested.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/overrides/{overrideId}/approve",
            new { notes = "Self approval must fail" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var platformOwner = await LoginAsync(
            client, "admin@sportsplatform.com", "Admin123!", "knockout-options-reviewer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", platformOwner.AccessToken);
        var approvedResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/overrides/{overrideId}/approve",
            new { notes = "Signed match report verified" });
        approvedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await approvedResponse.Content.ReadAsStringAsync());

        var history = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/competitions/{competitionId}/playoffs/overrides");
        history.Should().ContainSingle();
        history![0].GetProperty("winnerId").GetGuid().Should().Be(requestedWinner);
        history[0].GetProperty("reviewedBy").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GroupKnockout_QualifiesOnFinalApprovalUsingConfiguredTieBreakers()
    {
        using var isolatedFactory = new ApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "group-knockout");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var competitionId = Guid.NewGuid();
        var participantIds = Enumerable.Range(0, 8)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{200 + index:D12}"))
            .ToArray();
        var reviewerId = Guid.NewGuid();

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Competitions.Add(new Competition(
                competitionId,
                tenantId,
                "Configured Groups Cup",
                Guid.Parse("00000000-0000-0000-0000-000000000100")));
            foreach (var participantId in participantIds)
            {
                var registration = Registration.CreateForCompetition(
                    Guid.NewGuid(), competitionId, participantId, RegistrationType.Team);
                registration.Submit();
            registration.Approve(reviewerId, "Eligible for reporting");
                database.Registrations.Add(registration);
            }
            await database.SaveChangesAsync();
        }

        var generatedResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/fixtures/group-knockout",
            new
            {
                editionName = "2026",
                groupStageName = "Groups",
                knockoutStageName = "Finals",
                bracketName = "Configured Groups Cup Finals",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                groupsCount = 2,
                teamsPerGroup = 4,
                qualifiersPerGroup = 1,
                bestRunnerUpCount = 1,
                tieBreakers = new[] { "points", "pointDifferential", "pointsFor", "wins" }
            });
        generatedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await generatedResponse.Content.ReadAsStringAsync());
        var generated = await generatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        generated.GetProperty("groupFixtureCount").GetInt32().Should().Be(12);
        generated.GetProperty("qualifierCount").GetInt32().Should().Be(3);
        generated.GetProperty("knockoutMatchCount").GetInt32().Should().Be(3);

        var pendingBracket = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/playoffs");
        pendingBracket.GetProperty("rounds").EnumerateArray().First()
            .GetProperty("matches").EnumerateArray()
            .Should().OnlyContain(match =>
                match.GetProperty("participant1Id").ValueKind == JsonValueKind.Null
                && match.GetProperty("participant2Id").ValueKind == JsonValueKind.Null);

        Guid[] lastResultIds;
        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var groupContests = await database.Contests
                .Include(item => item.Participants)
                .Where(item => item.StageId == generated.GetProperty("groupStageId").GetGuid())
                .OrderBy(item => item.GroupId)
                .ThenBy(item => item.Id)
                .ToArrayAsync();
            var groupIds = groupContests.Select(item => item.GroupId!.Value).Distinct().Order().ToArray();
            var allResults = new List<(Contest Contest, ContestResult[] Results)>();
            foreach (var (groupId, groupIndex) in groupIds.Select((id, index) => (id, index)))
            {
                var groupParticipantIds = groupContests
                    .Where(item => item.GroupId == groupId)
                    .SelectMany(item => item.Participants.Select(entry => entry.ParticipantId))
                    .Distinct()
                    .Order()
                    .ToArray();
                var a = groupParticipantIds[0];
                var b = groupParticipantIds[1];
                var c = groupParticipantIds[2];
                var d = groupParticipantIds[3];
                foreach (var contest in groupContests.Where(item => item.GroupId == groupId))
                {
                    var ids = contest.Participants.Select(item => item.ParticipantId).ToHashSet();
                    Guid winner;
                    decimal winnerScore;
                    decimal loserScore = 0;
                    if (ids.SetEquals(new[] { a, b })) { winner = a; winnerScore = 5; }
                    else if (ids.SetEquals(new[] { a, c })) { winner = c; winnerScore = 1; }
                    else if (ids.SetEquals(new[] { a, d })) { winner = a; winnerScore = 1; }
                    else if (ids.SetEquals(new[] { b, c }))
                    {
                        winner = b;
                        winnerScore = groupIndex == 0 ? 2 : 5;
                    }
                    else if (ids.SetEquals(new[] { b, d })) { winner = b; winnerScore = 1; }
                    else { winner = c; winnerScore = 1; }
                    var loser = ids.Single(item => item != winner);
                    var winnerResult = new ContestResult(Guid.NewGuid(), contest.Id, Guid.NewGuid(), winner);
                    winnerResult.SetScore(winnerScore, winnerScore.ToString(), null);
                    winnerResult.SetRank(1, "1");
                    winnerResult.Submit();
                    var loserResult = new ContestResult(Guid.NewGuid(), contest.Id, Guid.NewGuid(), loser);
                    loserResult.SetScore(loserScore, "0", null);
                    loserResult.SetRank(2, "2");
                    loserResult.Submit();
                    allResults.Add((contest, new[] { winnerResult, loserResult }));
                }
            }
            var last = allResults[^1];
            foreach (var item in allResults.SkipLast(1))
            {
                foreach (var result in item.Results) result.Approve(reviewerId);
                item.Contest.SetWinner(item.Results[0].ParticipantId);
                item.Contest.Start();
                item.Contest.Complete();
                item.Contest.SubmitForApproval();
                item.Contest.Approve();
            }
            last.Contest.Start();
            last.Contest.Complete();
            last.Contest.SubmitForApproval();
            database.ContestResults.AddRange(allResults.SelectMany(item => item.Results));
            await database.SaveChangesAsync();
            lastResultIds = last.Results.Select(item => item.Id).ToArray();
        }

        (await client.PostAsJsonAsync(
            $"/api/results/{lastResultIds[0]}/approve",
            new { approvedBy = reviewerId })).StatusCode.Should().Be(HttpStatusCode.OK);
        var stillPending = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/playoffs");
        stillPending.GetProperty("rounds").EnumerateArray().First()
            .GetProperty("matches").EnumerateArray()
            .Should().OnlyContain(match =>
                match.GetProperty("participant1Id").ValueKind == JsonValueKind.Null
                && match.GetProperty("participant2Id").ValueKind == JsonValueKind.Null);

        var finalApproval = await client.PostAsJsonAsync(
            $"/api/results/{lastResultIds[1]}/approve",
            new { approvedBy = reviewerId });
        finalApproval.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await finalApproval.Content.ReadAsStringAsync());
        var qualifiedBracket = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/playoffs");
        var firstRound = qualifiedBracket.GetProperty("rounds").EnumerateArray().First()
            .GetProperty("matches").EnumerateArray().ToArray();
        using var verificationScope = isolatedFactory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider
            .GetRequiredService<SportsPlatformDbContext>();
        var persistedStandings = await verificationDatabase.Standings
            .Where(item => item.CompetitionId == competitionId)
            .ToArrayAsync();
        var expectedBestRunnerUp = persistedStandings.GroupBy(item => item.GroupId)
            .Select(group => group
                .OrderByDescending(item => item.Points)
                .ThenByDescending(item => item.PointDifferential)
                .ThenByDescending(item => item.PointsFor)
                .ThenByDescending(item => item.Won)
                .ThenBy(item => item.ParticipantId)
                .Skip(1)
                .First())
            .OrderByDescending(item => item.Points)
            .ThenByDescending(item => item.PointDifferential)
            .ThenByDescending(item => item.PointsFor)
            .ThenByDescending(item => item.Won)
            .ThenBy(item => item.ParticipantId)
            .First().ParticipantId;
        firstRound.SelectMany(match => new[]
            {
                match.GetProperty("participant1Id"),
                match.GetProperty("participant2Id")
            })
            .Where(item => item.ValueKind != JsonValueKind.Null)
            .Select(item => item.GetGuid()).Should().Contain(expectedBestRunnerUp);
        firstRound.Should().ContainSingle(match =>
            match.GetProperty("winnerId").ValueKind != JsonValueKind.Null);

        var persistedStage = await verificationDatabase.Competitions
            .AsNoTracking()
            .SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .SingleAsync(item => item.Id == generated.GetProperty("groupStageId").GetGuid());
        persistedStage.QualificationAppliedAt.Should().NotBeNull();
        (await verificationDatabase.Standings
            .Where(item => item.CompetitionId == competitionId)
            .Select(item => item.GroupId)
            .Distinct()
            .CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SeriesPlayoffs_SupportBestOfFiveAndSevenWithPersistedHomeSequences()
    {
        using var isolatedFactory = new ApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "long-series");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var competitionId = Guid.NewGuid();
        var participantIds = Enumerable.Range(0, 4)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{200 + index:D12}"))
            .ToArray();

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var competition = new Competition(
                competitionId,
                tenantId,
                "Long Series Cup",
                Guid.Parse("00000000-0000-0000-0000-000000000101"));
            competition.Publish();
            competition.AddEdition("2026", DateTime.UtcNow, DateTime.UtcNow.AddMonths(2));
            database.Competitions.Add(competition);
            foreach (var (participantId, index) in participantIds.Select((id, index) => (id, index)))
            {
                var standing = new Standing(Guid.NewGuid(), competitionId, Guid.Empty, participantId);
                standing.UpdateStats(3, 3 - index, 0, index, 20 - index, index, 9 - index);
                standing.SetPosition(index + 1);
                database.Standings.Add(standing);
            }
            await database.SaveChangesAsync();
        }

        var generatedResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/top-four",
            new
            {
                bracketName = "Long Series Finals",
                stageName = "Series playoffs",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                semifinalBestOf = 5,
                finalBestOf = 7,
                semifinalHomeSequence = "H,H,A,A,H",
                finalHomeSequence = "H,H,A,A,H,A,H",
                daysBetweenSeriesGames = 2
            });
        generatedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await generatedResponse.Content.ReadAsStringAsync());
        var generated = await generatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var semifinals = generated.GetProperty("semiFinalSeries").EnumerateArray().ToArray();
        semifinals.Should().HaveCount(2);
        semifinals.Should().OnlyContain(item => item.GetProperty("contestIds").GetArrayLength() == 5);
        generated.GetProperty("finalSeries").GetProperty("contestIds")
            .GetArrayLength().Should().Be(7);

        async Task<Guid> CompleteSeriesAsync(Guid[] contestIds, int winsRequired)
        {
            Guid winnerId;
            Guid[] decidingResultIds;
            using (var scope = isolatedFactory.Services.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                var games = await database.Contests
                    .Include(item => item.Participants)
                    .Where(item => contestIds.Contains(item.Id))
                    .OrderBy(item => item.SeriesGameNumber)
                    .ToArrayAsync();
                winnerId = games[0].Participants
                    .Single(item => item.ParticipantId == games[0].Participants
                        .OrderBy(entry => entry.ParticipantId).First().ParticipantId)
                    .ParticipantId;
                for (var index = 0; index < winsRequired - 1; index++)
                {
                    games[index].SetWinner(winnerId);
                    games[index].Start();
                    games[index].Complete();
                    games[index].SubmitForApproval();
                    games[index].Approve();
                }
                var decidingGame = games[winsRequired - 1];
                var loserId = decidingGame.Participants
                    .Single(item => item.ParticipantId != winnerId).ParticipantId;
                var winnerResult = new ContestResult(
                    Guid.NewGuid(), decidingGame.Id, Guid.NewGuid(), winnerId);
                winnerResult.SetScore(10, "10", null);
                winnerResult.SetRank(1, "1");
                winnerResult.Submit();
                var loserResult = new ContestResult(
                    Guid.NewGuid(), decidingGame.Id, Guid.NewGuid(), loserId);
                loserResult.SetScore(5, "5", null);
                loserResult.SetRank(2, "2");
                loserResult.Submit();
                decidingGame.Start();
                decidingGame.Complete();
                decidingGame.SubmitForApproval();
                database.ContestResults.AddRange(winnerResult, loserResult);
                await database.SaveChangesAsync();
                decidingResultIds = new[] { winnerResult.Id, loserResult.Id };
            }
            foreach (var resultId in decidingResultIds)
            {
                var approval = await client.PostAsJsonAsync(
                    $"/api/results/{resultId}/approve",
                    new { approvedBy = Guid.NewGuid() });
                approval.StatusCode.Should().Be(
                    HttpStatusCode.OK,
                    await approval.Content.ReadAsStringAsync());
            }
            return winnerId;
        }

        var semifinalWinners = new List<Guid>();
        foreach (var semifinal in semifinals)
        {
            var contestIds = semifinal.GetProperty("contestIds").EnumerateArray()
                .Select(item => item.GetGuid()).ToArray();
            semifinalWinners.Add(await CompleteSeriesAsync(contestIds, 3));
            using var scope = isolatedFactory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            (await database.Contests.Where(item => contestIds.Contains(item.Id)
                    && item.Status == ContestStatus.Cancelled).CountAsync())
                .Should().Be(2);
        }

        var finalContestIds = generated.GetProperty("finalSeries").GetProperty("contestIds")
            .EnumerateArray().Select(item => item.GetGuid()).ToArray();
        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var finalGames = await database.Contests.Include(item => item.Participants)
                .Where(item => finalContestIds.Contains(item.Id))
                .OrderBy(item => item.SeriesGameNumber)
                .ToArrayAsync();
            var expectedSequence = new[] { 1, 1, 2, 2, 1, 2, 1 };
            finalGames.Should().OnlyContain(item => item.Participants.Count == 2);
            finalGames.Select(item => item.SeriesHomeParticipantSlot)
                .Should().Equal(expectedSequence.Select(item => (int?)item));
            finalGames.Select(item => item.Participants.Single(entry => entry.SortOrder == 1).ParticipantId)
                .Should().Equal(expectedSequence.Select(slot => semifinalWinners[slot - 1]));
        }

        var championId = await CompleteSeriesAsync(finalContestIds, 4);
        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            (await database.Contests.Where(item => finalContestIds.Contains(item.Id)
                    && item.Status == ContestStatus.Cancelled).CountAsync())
                .Should().Be(3);
        }
        var publicPlayoffs = await client.GetFromJsonAsync<JsonElement>(
            $"/api/public/tenants/premier-league/playoffs?competitionId={competitionId}");
        publicPlayoffs.GetProperty("championId").GetGuid().Should().Be(championId);
        var publicFinal = publicPlayoffs.GetProperty("rounds")[1]
            .GetProperty("matches")[0];
        publicFinal.GetProperty("bestOf").GetInt32().Should().Be(7);
        publicFinal.GetProperty("score1").GetString().Should().Be("4");
        publicFinal.GetProperty("seriesGames").EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("homeParticipantName").ValueKind
                == JsonValueKind.String);
    }

    [Fact]
    public async Task KnockoutPlayoffs_PersistFiveQualifierBracketAndAdvanceByes()
    {
        using var isolatedFactory = new ApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var tenantAdmin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "knockout-byes");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAdmin.AccessToken);
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var competitionId = Guid.NewGuid();
        var participantIds = Enumerable.Range(0, 5)
            .Select(index => Guid.Parse($"00000000-0000-0000-0000-{200 + index:D12}"))
            .ToArray();

        using (var scope = isolatedFactory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var competition = new Competition(
                competitionId,
                tenantId,
                "Five Team Cup",
                Guid.Parse("00000000-0000-0000-0000-000000000100"));
            competition.AddEdition("2026", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
            database.Competitions.Add(competition);
            foreach (var (participantId, index) in participantIds.Select((id, index) => (id, index)))
            {
                var standing = new Standing(Guid.NewGuid(), competitionId, Guid.Empty, participantId);
                standing.UpdateStats(4, 4 - index, 0, index, 20 - index, index, 15 - index);
                standing.SetPosition(index + 1);
                database.Standings.Add(standing);
            }
            await database.SaveChangesAsync();
        }

        var generatedResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/playoffs/knockout",
            new
            {
                bracketName = "Five Team Finals",
                stageName = "Knockout",
                startTimeUtc = DateTime.UtcNow.AddDays(1),
                qualifierCount = 5,
                legsPerTie = 2,
                includeThirdPlace = true,
                randomDraw = true,
                drawSeed = 20260802
            });
        generatedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await generatedResponse.Content.ReadAsStringAsync());
        var generated = await generatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        generated.GetProperty("size").GetInt32().Should().Be(8);
        generated.GetProperty("qualifierCount").GetInt32().Should().Be(5);
        generated.GetProperty("byeCount").GetInt32().Should().Be(3);
        generated.GetProperty("contestCount").GetInt32().Should().Be(15);
        generated.GetProperty("isSeeded").GetBoolean().Should().BeFalse();
        generated.GetProperty("drawSeed").GetInt32().Should().Be(20260802);

        var bracket = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/playoffs");
        bracket.GetProperty("drawSeed").GetInt32().Should().Be(20260802);
        var rounds = bracket.GetProperty("rounds").EnumerateArray()
            .OrderBy(round => round.GetProperty("roundNumber").GetInt32())
            .ToArray();
        rounds.Should().HaveCount(3);
        var firstRound = rounds[0].GetProperty("matches").EnumerateArray().ToArray();
        firstRound.Should().HaveCount(4);
        firstRound.Count(match => match.GetProperty("winnerId").ValueKind != JsonValueKind.Null)
            .Should().Be(3);
        var semifinals = rounds[1].GetProperty("matches").EnumerateArray().ToArray();
        semifinals.Should().HaveCount(2);
        semifinals.Count(match => match.GetProperty("participant1Id").ValueKind != JsonValueKind.Null
            || match.GetProperty("participant2Id").ValueKind != JsonValueKind.Null).Should().Be(2);
        rounds[2].GetProperty("matches").EnumerateArray()
            .Should().ContainSingle(match => match.GetProperty("isThirdPlace").GetBoolean());

        using var verificationScope = isolatedFactory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider
            .GetRequiredService<SportsPlatformDbContext>();
        var stageIds = await verificationDatabase.Competitions
            .Where(item => item.Id == competitionId)
            .SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .Select(item => item.Id)
            .ToArrayAsync();
        var contests = await verificationDatabase.Contests
            .Include(contest => contest.Participants)
            .Where(contest => stageIds.Contains(contest.StageId!.Value))
            .ToArrayAsync();
        contests.Count(contest => contest.Status.ToString() == "Cancelled").Should().Be(6);
        contests.Where(contest => contest.SeriesGameNumber == 2 && contest.Participants.Count > 0)
            .Should().OnlyContain(contest => contest.Participants.Any(participant => participant.SortOrder == 2));
    }

    [Fact]
    public async Task SchedulingWorkflow_BlocksPublish_AllowsReasonedOverrides_AndNotifiesChanges()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-scheduling");
        var reviewer = await LoginAsync(
            client,
            "admin@premier.com",
            "TenantAdmin123!",
            "api-integration-scheduling-reviewer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var competitionId = Guid.NewGuid();
        var firstContestId = Guid.NewGuid();
        var secondContestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var sportId = await database.Sports.Select(item => item.Id).FirstAsync();
            var competition = new Competition(
                competitionId, reviewer.TenantId, "Scheduling verification", sportId);
            var edition = competition.AddEdition(
                "2026", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
            var stage = edition.AddStage("League", StageType.League, 1);
            var participantId = Guid.NewGuid();
            var officialId = Guid.NewGuid();
            var start = DateTime.UtcNow.Date.AddDays(3).AddHours(10);
            var first = new Contest(firstContestId, reviewer.TenantId, sportId, "HeadToHead");
            first.AssignToStage(stage.Id, 1);
            first.SetName("First fixture");
            first.AddParticipant(participantId, "Team");
            first.AddOfficial(officialId, "Referee");
            first.Schedule("Central Ground", start, 90);
            var second = new Contest(secondContestId, reviewer.TenantId, sportId, "HeadToHead");
            second.AssignToStage(stage.Id, 2);
            second.SetName("Second fixture");
            second.AddParticipant(participantId, "Team");
            second.AddOfficial(officialId, "Referee");
            second.Schedule("Central Ground", start.AddMinutes(30), 90);
            database.Competitions.Add(competition);
            database.Contests.AddRange(first, second);
            await database.SaveChangesAsync();
        }

        var blocked = await client.PostAsync(
            $"/api/competitions/{competitionId}/publish", null);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var blockedPayload = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        blockedPayload.GetProperty("error").GetString().Should().Be("ScheduleConflicts");

        var validation = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/schedule/validation");
        validation.GetProperty("canPublish").GetBoolean().Should().BeFalse();
        var conflicts = validation.GetProperty("conflicts").EnumerateArray().ToArray();
        conflicts.Select(item => item.GetProperty("type").GetString()).Should().Contain(
            new[] { "VenueConflict", "ParticipantConflict", "MinimumRest", "OfficialConflict" });
        foreach (var conflict in conflicts)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/competitions/{competitionId}/schedule/overrides",
                new
                {
                    conflictKey = conflict.GetProperty("key").GetString(),
                    reason = "Approved venue operations plan"
                });
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var published = await client.PostAsync(
            $"/api/competitions/{competitionId}/publish", null);
        published.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var requested = await client.PutAsJsonAsync(
            $"/api/competitions/{competitionId}/schedule/contests/{secondContestId}",
            new
            {
                venue = "North Ground",
                startTime = DateTime.UtcNow.Date.AddDays(4).AddHours(14),
                durationMinutes = 90,
                reason = "Move away from the shared venue window"
            });
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var adjustment = await requested.Content.ReadFromJsonAsync<JsonElement>();
        var adjustmentId = adjustment.GetProperty("id").GetGuid();

        var selfApproval = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/schedule/adjustments/{adjustmentId}/approve",
            new { notes = "Requester cannot approve" });
        selfApproval.StatusCode.Should().Be(HttpStatusCode.Conflict);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", reviewer.AccessToken);
        var approved = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/schedule/adjustments/{adjustmentId}/approve",
            new { notes = "Venue manager confirmed availability" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            (await database.Notifications.AnyAsync(item =>
                item.Metadata != null && item.Metadata.Contains(competitionId.ToString())))
                .Should().BeTrue();
            (await database.ScheduleConflictOverrides.AnyAsync(item =>
                item.CompetitionId == competitionId)).Should().BeFalse();

            var contests = await database.Contests
                .Where(item => item.Id == firstContestId || item.Id == secondContestId)
                .ToArrayAsync();
            var competition = await database.Competitions
                .Include(item => item.Editions)
                .ThenInclude(item => item.Stages)
                .SingleAsync(item => item.Id == competitionId);
            var notifications = await database.Notifications
                .Where(item => item.Metadata != null
                    && item.Metadata.Contains(competitionId.ToString()))
                .ToArrayAsync();
            var adjustments = await database.FixtureAdjustments
                .Where(item => item.CompetitionId == competitionId)
                .ToArrayAsync();
            database.Notifications.RemoveRange(notifications);
            database.FixtureAdjustments.RemoveRange(adjustments);
            database.Contests.RemoveRange(contests);
            database.Competitions.Remove(competition);
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ContestLifecycle_EnforcesSportPolicyAndPersistsActorAuditHistory()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-contest-lifecycle");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var sportId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Sports.Add(new Sport(sportId, $"LIFE-{sportId:N}", "Lifecycle test sport"));
            var contest = new Contest(contestId, admin.TenantId, sportId, "HeadToHead");
            contest.SetName("Lifecycle verification contest");
            contest.Schedule("Lifecycle Ground", DateTime.UtcNow.AddDays(1), 90);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        var policy = await client.PutAsJsonAsync(
            $"/api/contest-lifecycle/policies/{sportId}",
            new
            {
                tenantId = admin.TenantId,
                transitions = new[]
                {
                    new { from = "Scheduled", to = "Suspended" },
                    new { from = "Suspended", to = "Abandoned" }
                }
            });
        policy.StatusCode.Should().Be(HttpStatusCode.OK, await policy.Content.ReadAsStringAsync());

        var invalid = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/transition",
            new { targetStatus = "Completed" });
        invalid.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var missingReason = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/transition",
            new { targetStatus = "Suspended" });
        missingReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var suspended = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/transition",
            new { targetStatus = "Suspended", reason = "Lightning safety hold" });
        suspended.StatusCode.Should().Be(HttpStatusCode.OK, await suspended.Content.ReadAsStringAsync());
        var abandoned = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/transition",
            new { targetStatus = "Abandoned", reason = "Unsafe weather continued" });
        abandoned.StatusCode.Should().Be(HttpStatusCode.OK, await abandoned.Content.ReadAsStringAsync());

        var lifecycle = await client.GetFromJsonAsync<JsonElement>(
            $"/api/contests/{contestId}/lifecycle");
        lifecycle.GetProperty("usesSportPolicy").GetBoolean().Should().BeTrue();
        lifecycle.GetProperty("allowedTransitions").GetArrayLength().Should().Be(0);
        var history = await client.GetFromJsonAsync<JsonElement[]>(
            $"/api/contests/{contestId}/lifecycle/history");
        history.Should().HaveCount(3);
        history!.Select(item => item.GetProperty("reason").ValueKind == JsonValueKind.Null
                ? null
                : item.GetProperty("reason").GetString())
            .Should().Contain(new[] { "Lightning safety hold", "Unsafe weather continued" });
        history!.Skip(1).Should().OnlyContain(item =>
            item.GetProperty("userId").GetGuid() != Guid.Empty);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.ContestLifecyclePolicies.RemoveRange(database.ContestLifecyclePolicies.Where(item =>
                item.TenantId == admin.TenantId && item.SportId == sportId));
            database.Contests.RemoveRange(database.Contests.Where(item => item.Id == contestId));
            database.Sports.RemoveRange(database.Sports.Where(item => item.Id == sportId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ContestCheckIn_RequiresReadiness_PreservesHistory_AndAllowsAuthorizedOverride()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-contest-check-in");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var sportId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var overrideContestId = Guid.NewGuid();
        var teamOne = Guid.NewGuid();
        var teamTwo = Guid.NewGuid();
        var athlete = Guid.NewGuid();
        var officialId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Sports.Add(new Sport(sportId, $"CHECKIN-{sportId:N}", "Check-in test sport"));
            var contest = new Contest(contestId, admin.TenantId, sportId, "MultiParticipant");
            contest.SetName("Readiness verification contest");
            contest.AddParticipant(teamOne, "Team");
            contest.AddParticipant(teamTwo, "Team");
            contest.AddParticipant(athlete, "Athlete");
            contest.AddOfficial(officialId, "Referee", "Readiness Referee");
            contest.Schedule("Readiness Ground", DateTime.UtcNow.AddDays(1), 90);
            var overrideContest = new Contest(
                overrideContestId, admin.TenantId, sportId, "HeadToHead");
            overrideContest.SetName("Override verification contest");
            overrideContest.AddParticipant(Guid.NewGuid(), "Athlete");
            overrideContest.Schedule("Override Ground", DateTime.UtcNow.AddDays(2), 60);
            database.Contests.AddRange(contest, overrideContest);
            await database.SaveChangesAsync();
        }

        (await client.PostAsync($"/api/checkin/contests/{contestId}/open", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var blockedStart = await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/transition", new { targetStatus = "InProgress" });
        blockedStart.StatusCode.Should().Be(HttpStatusCode.Conflict);

        foreach (var participantId in new[] { teamOne, teamTwo, athlete })
        {
            var response = await client.PostAsync(
                $"/api/checkin/contests/{contestId}/participants/{participantId}/checkin", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                await response.Content.ReadAsStringAsync());
        }
        (await client.PostAsync(
            $"/api/checkin/contests/{contestId}/roster/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync(
            $"/api/checkin/contests/{contestId}/officials/{officialId}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var incomplete = await client.PostAsync(
            $"/api/checkin/contests/{contestId}/complete", null);
        incomplete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync(
            $"/api/checkin/contests/{contestId}/venue/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await client.PostAsync(
            $"/api/checkin/contests/{contestId}/complete", null);
        completed.StatusCode.Should().Be(HttpStatusCode.OK,
            await completed.Content.ReadAsStringAsync());
        var completedState = await completed.Content.ReadFromJsonAsync<JsonElement>();
        completedState.GetProperty("canStart").GetBoolean().Should().BeTrue();
        completedState.GetProperty("contestStatus").GetInt32()
            .Should().Be((int)ContestStatus.Ready);
        (await client.PostAsJsonAsync(
            $"/api/contests/{contestId}/transition", new { targetStatus = "InProgress" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync(
            $"/api/checkin/contests/{overrideContestId}/open", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var missingReason = await client.PostAsJsonAsync(
            $"/api/checkin/contests/{overrideContestId}/override", new { reason = "" });
        missingReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var overridden = await client.PostAsJsonAsync(
            $"/api/checkin/contests/{overrideContestId}/override",
            new { reason = "Emergency event-director authorization" });
        overridden.StatusCode.Should().Be(HttpStatusCode.OK,
            await overridden.Content.ReadAsStringAsync());
        var overrideState = await overridden.Content.ReadFromJsonAsync<JsonElement>();
        overrideState.GetProperty("isOverridden").GetBoolean().Should().BeTrue();
        overrideState.GetProperty("canStart").GetBoolean().Should().BeTrue();
        (await client.PostAsJsonAsync(
            $"/api/contests/{overrideContestId}/transition",
            new { targetStatus = "InProgress" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await client.GetFromJsonAsync<JsonElement>(
            $"/api/checkin/contests/{contestId}/history");
        var actions = history.EnumerateArray().Select(item =>
            item.GetProperty("action").GetString()).ToArray();
        actions.Should().Contain(new[]
        {
            "CheckInOpened", "ParticipantCheckedIn", "RosterConfirmed",
            "OfficialConfirmed", "VenueConfirmed", "CheckInCompleted"
        });
        history.EnumerateArray().Should().OnlyContain(item =>
            item.GetProperty("actorId").GetGuid() != Guid.Empty);
        var overrideHistory = await client.GetFromJsonAsync<JsonElement>(
            $"/api/checkin/contests/{overrideContestId}/history");
        overrideHistory.EnumerateArray().Should().Contain(item =>
            item.GetProperty("isOverride").GetBoolean()
            && item.GetProperty("reason").GetString() ==
                "Emergency event-director authorization");

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.ContestCheckInHistory.RemoveRange(
                database.ContestCheckInHistory.Where(item =>
                    item.ContestId == contestId || item.ContestId == overrideContestId));
            database.ContestReadiness.RemoveRange(
                database.ContestReadiness.Where(item =>
                    item.ContestId == contestId || item.ContestId == overrideContestId));
            database.Contests.RemoveRange(database.Contests.Where(item =>
                item.Id == contestId || item.Id == overrideContestId));
            database.Sports.RemoveRange(database.Sports.Where(item => item.Id == sportId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task LiveEventApi_PersistsCompleteContract_RejectsSequence_UsesPinnedRuleset_AndReplaysOriginal()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "api-integration-complete-live-event");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var sportId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        var contestId = Guid.NewGuid();
        var firstParticipantId = Guid.NewGuid();
        var secondParticipantId = Guid.NewGuid();
        var segmentId = Guid.Empty;
        var rulesetId = Guid.Empty;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var sport = await database.Sports.Include(item => item.Versions)
                .SingleAsync(item => item.Id == sportId);
            var ruleset = sport.CreateNewVersion(
                $"Live event verification {contestId:N}", "integration-test");
            ruleset.ConfigureContestShape(
                "TeamVsTeam",
                "Team",
                "[]",
                "[{\"id\":\"GOAL\",\"name\":\"Configured goal\",\"segmentApplicability\":[\"Half\"],\"requiresParticipant\":true,\"requiresSecondaryParticipant\":true,\"requiredMetric\":\"score\",\"timingRequirement\":\"Timestamp\",\"correctionPolicy\":\"Prohibited\",\"validation\":{\"minimumValue\":1,\"maximumValue\":1}}]",
                "[{\"id\":\"score\",\"name\":\"Score\",\"type\":\"Integer\",\"validation\":{\"minimum\":0}}]",
                "[]",
                "[]",
                "{}",
                "{}");
            ruleset.Publish();
            rulesetId = ruleset.Id;
            database.Entry(ruleset).State = EntityState.Added;
            var contest = new Contest(contestId, admin.TenantId, sportId, "TeamVsTeam");
            contest.SetName("Complete live event verification");
            contest.AddParticipant(firstParticipantId, "Team");
            contest.AddParticipant(secondParticipantId, "Team");
            segmentId = contest.AddSegment("First Half", "Half", 1).Id;
            contest.AssignRulesetVersion(rulesetId);
            contest.Schedule("Live Event Ground", DateTime.UtcNow.AddHours(2), 90);
            database.Contests.Add(contest);
            await database.SaveChangesAsync();
        }

        var eventId = Guid.NewGuid();
        var deviceTimestamp = DateTime.UtcNow.AddSeconds(-10);
        object Request(Guid id, int sequence, string idempotencyKey, string eventType = "GOAL") => new
        {
            eventId = id,
            eventType,
            participantId = firstParticipantId,
            secondaryParticipantId = secondParticipantId,
            metricName = "score",
            value = 1,
            segmentId,
            deviceTimestamp,
            deviceId = "integration-device-53",
            sequenceNumber = sequence,
            idempotencyKey
        };

        var schemaResponse = await client.GetAsync(
            $"/api/livescoring/contests/{contestId}/schema");
        schemaResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await schemaResponse.Content.ReadAsStringAsync());
        var scoringSchema = await schemaResponse.Content.ReadFromJsonAsync<JsonElement>();
        var configuredEvent = scoringSchema.GetProperty("eventDefinitions").EnumerateArray().Single();
        configuredEvent.GetProperty("code").GetString().Should().Be("GOAL");
        configuredEvent.GetProperty("requiredMetric").GetString().Should().Be("score");
        configuredEvent.GetProperty("requiresSecondaryParticipant").GetBoolean().Should().BeTrue();
        configuredEvent.GetProperty("timingRequirement").GetString().Should().Be("Timestamp");
        configuredEvent.GetProperty("correctionPolicy").GetString().Should().Be("Prohibited");
        scoringSchema.GetProperty("actions").EnumerateArray().Should().Contain(item =>
            item.GetProperty("eventType").GetString() == "GOAL");

        var incomplete = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            new
            {
                eventId = Guid.NewGuid(), eventType = "GOAL",
                participantId = firstParticipantId, metricName = "score", value = 1,
                segmentId, deviceTimestamp, deviceId = "integration-device-invalid",
                sequenceNumber = 1, idempotencyKey = "missing-secondary"
            });
        incomplete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await incomplete.Content.ReadAsStringAsync()).Should().Contain("secondary participant");

        var outOfSequence = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Request(Guid.NewGuid(), 2, "sequence-gap"));
        outOfSequence.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var conflictScope = _factory.Services.CreateScope())
        {
            var database = conflictScope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            (await database.AuditEntries.AsNoTracking().AnyAsync(item =>
                item.Action == "LiveScoreSyncConflict"
                && item.EntityId == contestId.ToString()
                && item.TenantId == admin.TenantId)).Should().BeTrue();
        }

        var first = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Request(eventId, 1, "stable-live-event"));
        first.StatusCode.Should().Be(HttpStatusCode.OK,
            await first.Content.ReadAsStringAsync());
        var firstPayload = await first.Content.ReadFromJsonAsync<JsonElement>();
        firstPayload.GetProperty("isReplay").GetBoolean().Should().BeFalse();
        var persisted = firstPayload.GetProperty("event");
        persisted.GetProperty("tenantId").GetGuid().Should().Be(admin.TenantId);
        persisted.GetProperty("contestId").GetGuid().Should().Be(contestId);
        persisted.GetProperty("eventId").GetGuid().Should().Be(eventId);
        persisted.GetProperty("eventType").GetString().Should().Be("GOAL");
        persisted.GetProperty("participantId").GetGuid().Should().Be(firstParticipantId);
        persisted.GetProperty("secondaryParticipantId").GetGuid().Should().Be(secondParticipantId);
        persisted.GetProperty("segmentId").GetGuid().Should().Be(segmentId);
        persisted.GetProperty("value").GetDecimal().Should().Be(1);
        persisted.GetProperty("deviceId").GetString().Should().Be("integration-device-53");
        persisted.GetProperty("sequence").GetInt32().Should().Be(1);
        persisted.GetProperty("idempotencyKey").GetString().Should().Be("stable-live-event");
        persisted.GetProperty("rulesetVersionId").GetGuid().Should().Be(rulesetId);
        persisted.GetProperty("scorerId").GetString().Should().NotBeNullOrWhiteSpace();
        persisted.GetProperty("deviceTimestamp").GetDateTime().Should()
            .BeCloseTo(deviceTimestamp, TimeSpan.FromSeconds(1));
        persisted.GetProperty("serverTimestamp").GetDateTime().Should().BeAfter(deviceTimestamp);
        var originalRecordId = persisted.GetProperty("id").GetGuid();

        var prohibitedCorrection = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events/{originalRecordId}/reverse",
            new { reason = "Must be rejected by the configured policy" });
        prohibitedCorrection.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await prohibitedCorrection.Content.ReadAsStringAsync()).Should().Contain("does not allow corrections");

        var replay = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Request(Guid.NewGuid(), 99, "stable-live-event"));
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayPayload = await replay.Content.ReadFromJsonAsync<JsonElement>();
        replayPayload.GetProperty("isReplay").GetBoolean().Should().BeTrue();
        replayPayload.GetProperty("event").GetProperty("id").GetGuid()
            .Should().Be(originalRecordId);

        var disallowed = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Request(Guid.NewGuid(), 2, "ruleset-rejection", "OWN_GOAL"));
        disallowed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await disallowed.Content.ReadAsStringAsync()).Should().Contain("ruleset version");

        var second = await client.PostAsJsonAsync(
            $"/api/livescoring/contests/{contestId}/events",
            Request(Guid.NewGuid(), 2, "second-live-event"));
        second.StatusCode.Should().Be(HttpStatusCode.OK,
            await second.Content.ReadAsStringAsync());
        var state = await client.GetFromJsonAsync<JsonElement>(
            $"/api/livescoring/contests/{contestId}/state");
        state.GetProperty("eventCount").GetInt32().Should().Be(2);
        state.GetProperty("rulesetVersionId").GetGuid().Should().Be(rulesetId);
        state.GetProperty("totals")[0].GetProperty("value").GetDecimal().Should().Be(2);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.LiveScores.RemoveRange(
                database.LiveScores.Where(item => item.ContestId == contestId));
            database.Contests.RemoveRange(
                database.Contests.Where(item => item.Id == contestId));
            var sport = await database.Sports.Include(item => item.Versions)
                .SingleAsync(item => item.Id == sportId);
            database.Remove(sport.Versions.Single(item => item.Id == rulesetId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PlatformDashboard_IsRoleRestrictedAndSupportActionsRequireConfirmationAndAudit()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "platform-dashboard-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);
        (await client.GetAsync("/api/platform-admin/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "platform-dashboard-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Support Tenant {suffix}",
            shortName = $"ST{suffix[..2]}",
            slug = $"support-{suffix}"
        });
        tenantResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await tenantResponse.Content.ReadAsStringAsync());
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenant.GetProperty("id").GetGuid();

        var dashboard = await client.GetFromJsonAsync<JsonElement>(
            "/api/platform-admin/dashboard");
        dashboard.GetProperty("health").GetProperty("status").GetString()
            .Should().Be("Healthy");
        dashboard.GetProperty("tenants").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetGuid() == tenantId);

        var unconfirmed = await client.PostAsJsonAsync(
            $"/api/platform-admin/tenants/{tenantId}/support-action",
            new
            {
                action = "Suspend",
                reason = "Investigating a tenant support incident",
                confirmed = false
            });
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var suspendResponse = await client.PostAsJsonAsync(
            $"/api/platform-admin/tenants/{tenantId}/support-action",
            new
            {
                action = "Suspend",
                reason = "Investigating a tenant support incident",
                confirmed = true
            });
        suspendResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await suspendResponse.Content.ReadAsStringAsync());
        var suspended = await suspendResponse.Content.ReadFromJsonAsync<JsonElement>();
        suspended.GetProperty("status").GetString().Should().Be("Suspended");
        var auditId = suspended.GetProperty("auditId").GetGuid();

        dashboard = await client.GetFromJsonAsync<JsonElement>(
            "/api/platform-admin/dashboard");
        dashboard.GetProperty("tenants").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == tenantId)
            .GetProperty("status").GetString().Should().Be("Suspended");
        var audit = dashboard.GetProperty("recentAudit").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == auditId);
        audit.GetProperty("action").GetString()
            .Should().Be("PlatformSupportSuspendedTenant");
        audit.GetProperty("reason").GetString()
            .Should().Be("Investigating a tenant support incident");

        var activateResponse = await client.PostAsJsonAsync(
            $"/api/platform-admin/tenants/{tenantId}/support-action",
            new
            {
                action = "Activate",
                reason = "Support incident resolved",
                confirmed = true
            });
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlatformSupport_CoversSearchJobsSyncHistoryAndNotificationControlsWithAudit()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            $"support-operations-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid reportId;
        Guid contestId;
        Guid eventRecordId;
        Guid conflictAuditId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var userId = await database.Users.Where(item => item.Email == "admin@sportsplatform.com")
                .Select(item => item.Id).SingleAsync();
            var report = new Report(
                Guid.NewGuid(), tenantId, userId,
                $"Failed support report {Guid.NewGuid():N}",
                ReportType.CompetitionSummary);
            report.SetFormat(ReportFormat.Csv);
            report.MarkAsFailed("Synthetic safe-job failure for support acceptance.");
            database.Reports.Add(report);
            reportId = report.Id;

            var contest = await database.Contests
                .Include(item => item.Participants)
                .FirstAsync(item => item.TenantId == tenantId && item.Participants.Count > 0);
            contestId = contest.Id;
            var participantId = contest.Participants.First().ParticipantId;
            var score = new LiveScore(
                Guid.NewGuid(), contest.Id, Guid.NewGuid(), participantId,
                "support_history", 3, tenantId: tenantId,
                eventType: "SUPPORT_HISTORY", deviceId: "support-acceptance",
                idempotencyKey: $"support-{Guid.NewGuid():N}");
            score.SetSequence(987654);
            database.LiveScores.Add(score);
            eventRecordId = score.Id;

            var conflict = new AuditEntry(
                Guid.NewGuid(), tenantId, userId,
                "LiveScoreSyncConflict", "Contest", contest.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    eventId = Guid.NewGuid(),
                    sequenceNumber = 4,
                    deviceId = "support-conflict-device"
                }),
                "Sequence number must be greater than the accepted server sequence.");
            database.AuditEntries.Add(conflict);
            conflictAuditId = conflict.Id;
            await database.SaveChangesAsync();
        }

        var dashboard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/platform-admin/dashboard?search=premier&historyContestId={contestId}");
        dashboard.GetProperty("tenants").EnumerateArray().Should().OnlyContain(item =>
            item.GetProperty("name").GetString()!.Contains("Premier", StringComparison.OrdinalIgnoreCase)
            || item.GetProperty("slug").GetString()!.Contains("premier", StringComparison.OrdinalIgnoreCase));
        dashboard.GetProperty("tenants").EnumerateArray().Should().Contain(item =>
            item.GetProperty("id").GetGuid() == tenantId
            && item.GetProperty("lease").ValueKind != JsonValueKind.Null);
        dashboard.GetProperty("failedJobs").EnumerateArray().Should().Contain(item =>
            item.GetProperty("id").GetGuid() == reportId
            && item.GetProperty("status").GetString() == "Failed");
        dashboard.GetProperty("syncConflicts").EnumerateArray().Should().Contain(item =>
            item.GetProperty("id").GetGuid() == conflictAuditId);
        var history = dashboard.GetProperty("eventHistory");
        history.GetProperty("id").GetGuid().Should().Be(contestId);
        history.GetProperty("events").EnumerateArray().Should().Contain(item =>
            item.GetProperty("id").GetGuid() == eventRecordId
            && item.GetProperty("deviceId").GetString() == "support-acceptance");

        var pauseWithoutConfirmation = await client.PostAsJsonAsync(
            $"/api/platform-admin/tenants/{tenantId}/notifications",
            new
            {
                disabled = true,
                disabledUntilUtc = DateTime.UtcNow.AddHours(1),
                reason = "Support notification incident",
                confirmed = false
            });
        pauseWithoutConfirmation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var pause = await client.PostAsJsonAsync(
            $"/api/platform-admin/tenants/{tenantId}/notifications",
            new
            {
                disabled = true,
                disabledUntilUtc = DateTime.UtcNow.AddHours(1),
                reason = "Support notification incident",
                confirmed = true
            });
        pause.StatusCode.Should().Be(HttpStatusCode.OK, await pause.Content.ReadAsStringAsync());
        var pausePayload = await pause.Content.ReadFromJsonAsync<JsonElement>();
        var pauseAuditId = pausePayload.GetProperty("auditId").GetGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var policy = scope.ServiceProvider
                .GetRequiredService<ITenantNotificationDeliveryPolicy>();
            (await policy.GetDisabledTenantIdsAsync(new[] { tenantId }, DateTime.UtcNow))
                .Should().Contain(tenantId);
        }

        var unconfirmedReprocess = await client.PostAsJsonAsync(
            $"/api/platform-admin/jobs/{reportId}/reprocess",
            new { reason = "Safe report retry", confirmed = false });
        unconfirmedReprocess.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reprocess = await client.PostAsJsonAsync(
            $"/api/platform-admin/jobs/{reportId}/reprocess",
            new { reason = "Safe report retry after reviewing the failure", confirmed = true });
        reprocess.StatusCode.Should().Be(HttpStatusCode.Accepted,
            await reprocess.Content.ReadAsStringAsync());
        var reprocessPayload = await reprocess.Content.ReadFromJsonAsync<JsonElement>();
        var reprocessAuditId = reprocessPayload.GetProperty("auditId").GetGuid();

        dashboard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/platform-admin/dashboard?tenantId={tenantId}");
        dashboard.GetProperty("tenants")[0].GetProperty("notifications")
            .GetProperty("disabled").GetBoolean().Should().BeTrue();

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var actions = await database.AuditEntries.AsNoTracking()
                .Where(item => item.Id == pauseAuditId || item.Id == reprocessAuditId)
                .ToDictionaryAsync(item => item.Id, item => item.Action);
            actions[pauseAuditId].Should().Be("PlatformSupportDisabledNotifications");
            actions[reprocessAuditId].Should().Be("PlatformSupportReprocessedReportJob");
        }

        var resume = await client.PostAsJsonAsync(
            $"/api/platform-admin/tenants/{tenantId}/notifications",
            new
            {
                disabled = false,
                disabledUntilUtc = (DateTime?)null,
                reason = "Support notification incident resolved",
                confirmed = true
            });
        resume.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = _factory.Services.CreateScope())
        {
            var policy = scope.ServiceProvider
                .GetRequiredService<ITenantNotificationDeliveryPolicy>();
            (await policy.GetDisabledTenantIdsAsync(new[] { tenantId }, DateTime.UtcNow))
                .Should().NotContain(tenantId);
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.LiveScores.Remove(await database.LiveScores.SingleAsync(item =>
                item.Id == eventRecordId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task TenantDashboard_IsTenantScopedAndReturnsPermissionAwareOperations()
    {
        using var client = _factory.CreateClient();
        var scorer = await LoginAsync(
            client,
            "scorer@premier.com",
            "Scorer123!",
            "tenant-dashboard-scorer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", scorer.AccessToken);

        var response = await client.GetAsync("/api/tenant-admin/dashboard");
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var dashboard = await response.Content.ReadFromJsonAsync<JsonElement>();
        dashboard.GetProperty("tenant").GetProperty("id").GetGuid()
            .Should().Be(scorer.TenantId);
        dashboard.GetProperty("branding").GetProperty("tenantName").GetString()
            .Should().NotBeNullOrWhiteSpace();

        var modules = dashboard.GetProperty("modules").EnumerateArray().ToArray();
        modules.Select(item => item.GetProperty("key").GetString()).Should().BeEquivalentTo(
            "branding", "sports", "participants", "registrations", "competitions",
            "fixtures", "officials", "live-contests", "results", "reports", "sponsors");
        modules.Single(item => item.GetProperty("key").GetString() == "live-contests")
            .GetProperty("accessible").GetBoolean().Should().BeTrue();
        modules.Where(item => !item.GetProperty("accessible").GetBoolean())
            .Should().OnlyContain(item => item.GetProperty("count").ValueKind == JsonValueKind.Null);

        (await client.GetAsync($"/api/tenant-admin/dashboard?tenantId={Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "tenant-dashboard-platform-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var platformResponse = await client.GetAsync(
            $"/api/tenant-admin/dashboard?tenantId={scorer.TenantId}");
        platformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuditHistory_IsAppendOnlySearchableTenantScopedAndCoversRequiredMutations()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var foreignTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", $"audit-history-{suffix}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var localization = await client.PutAsJsonAsync(
            $"/api/tenants/{tenantId}/localization", new
            {
                country = "Kenya",
                timezone = "Africa/Nairobi",
                currency = "KES",
                defaultLanguage = "en"
            });
        localization.StatusCode.Should().Be(
            HttpStatusCode.OK, await localization.Content.ReadAsStringAsync());

        Guid competitionId;
        Guid contestId;
        Guid contestParticipantId;
        Guid adminUserId;
        string originalEntitlements;
        string originalUsageLimits;
        Guid subscriptionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            competitionId = await database.Competitions.AsNoTracking()
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id).FirstAsync();
            var contest = await database.Contests.AsNoTracking()
                .Include(item => item.Participants)
                .FirstAsync(item => item.TenantId == tenantId && item.Participants.Count > 0);
            contestId = contest.Id;
            contestParticipantId = contest.Participants.First().ParticipantId;
            adminUserId = await database.Users.AsNoTracking()
                .Where(item => item.Email == "admin@premier.com")
                .Select(item => item.Id).SingleAsync();
            var subscription = await database.Subscriptions.AsNoTracking()
                .FirstAsync(item => item.TenantId == tenantId
                    && item.Status != SubscriptionStatus.Draft
                    && item.Status != SubscriptionStatus.Archived);
            subscriptionId = subscription.Id;
            originalEntitlements = subscription.FeatureEntitlements;
            originalUsageLimits = subscription.UsageLimits;
        }

        var schedule = await client.PutAsJsonAsync(
            $"/api/competitions/{competitionId}/schedule/constraints", new
            {
                defaultDurationMinutes = 100,
                minimumRestMinutes = 45,
                venueTurnaroundMinutes = 10,
                windowStartMinutes = 360,
                windowEndMinutes = 1320,
                blackoutDates = Array.Empty<string>()
            });
        schedule.StatusCode.Should().Be(
            HttpStatusCode.OK, await schedule.Content.ReadAsStringAsync());

        var roleCreate = await client.PostAsJsonAsync("/api/roles", new
        {
            name = $"Audit Reviewer {suffix}",
            description = "Audit acceptance role",
            permissions = new[] { "audit.read" }
        });
        roleCreate.StatusCode.Should().Be(
            HttpStatusCode.Created, await roleCreate.Content.ReadAsStringAsync());

        var participant = new Team(Guid.NewGuid(), tenantId, $"Audit Team {suffix}", $"AT{suffix[..2]}");
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Participants.Add(participant);
            await database.SaveChangesAsync();
        }
        var registrationCreate = await client.PostAsJsonAsync(
            $"/api/registrations/competition/{competitionId}", new
            {
                participantId = participant.Id,
                type = (int)RegistrationType.Team
            });
        registrationCreate.StatusCode.Should().Be(
            HttpStatusCode.Created, await registrationCreate.Content.ReadAsStringAsync());
        var registrationId = (await registrationCreate.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        (await client.PostAsync($"/api/registrations/{registrationId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var approvalReason = $"Eligibility verified {suffix}";
        var approve = await client.PostAsJsonAsync(
            $"/api/registrations/{registrationId}/approve", new { notes = approvalReason });
        approve.StatusCode.Should().Be(
            HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());

        var entitlementUpdate = await client.PutAsJsonAsync(
            $"/api/subscriptions/{subscriptionId}/entitlements",
            new Dictionary<string, bool> { ["sponsorship.placements"] = true });
        entitlementUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid liveAuditId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var nextSequence = await database.LiveScores
                .Where(item => item.ContestId == contestId)
                .Select(item => (int?)item.Sequence).MaxAsync() ?? 0;
            var score = new LiveScore(
                Guid.NewGuid(), contestId, Guid.NewGuid(), contestParticipantId,
                "audit_marker", 1, tenantId: tenantId,
                eventType: "AUDIT_MARKER", deviceId: "audit-acceptance",
                idempotencyKey: $"audit-{suffix}");
            score.SetSequence(nextSequence + 1);
            score.SetScorer(adminUserId.ToString(), null);
            database.LiveScores.Add(score);
            await database.SaveChangesAsync();
            liveAuditId = await database.AuditEntries.AsNoTracking()
                .Where(item => item.Action == "LiveScoreRecorded"
                    && item.EntityId == score.Id.ToString())
                .Select(item => item.Id).SingleAsync();
        }

        var decisionSearch = await client.GetAsync(
            $"/api/audit?tenantId={tenantId}&action=RegistrationApproved&search={suffix}");
        decisionSearch.StatusCode.Should().Be(
            HttpStatusCode.OK, await decisionSearch.Content.ReadAsStringAsync());
        var page = await decisionSearch.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        var decision = page.GetProperty("items").EnumerateArray().First();
        decision.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        decision.GetProperty("userId").GetGuid().Should().Be(adminUserId);
        decision.GetProperty("action").GetString().Should().Be("RegistrationApproved");
        decision.GetProperty("entityType").GetString().Should().Be("Registration");
        decision.GetProperty("entityId").GetString().Should().Be(registrationId.ToString());
        decision.GetProperty("reason").GetString().Should().Be(approvalReason);
        decision.GetProperty("timestamp").GetDateTime().Should().BeCloseTo(
            DateTime.UtcNow, TimeSpan.FromMinutes(2));

        (await client.GetAsync($"/api/audit?tenantId={foreignTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var actions = await database.AuditEntries.AsNoTracking()
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Action).ToArrayAsync();
            actions.Should().Contain(new[]
            {
                "AuthenticationLoginSucceeded",
                "TenantLocalizationUpdated",
                "PermissionRoleCreated",
                "RegistrationApproved",
                "ScheduleConstraintsConfigured",
                "LiveScoreRecorded",
                "LeaseEntitlementsUpdated"
            });
            var immutable = await database.AuditEntries.SingleAsync(item => item.Id == liveAuditId);
            database.AuditEntries.Remove(immutable);
            var mutation = () => database.SaveChangesAsync();
            await mutation.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*append-only*");
            database.Entry(immutable).State = EntityState.Detached;

            var subscription = await database.Subscriptions.SingleAsync(item => item.Id == subscriptionId);
            subscription.ConfigureFeatures(originalEntitlements, originalUsageLimits);
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PrivacyControls_AreDurableTenantScopedAuditedAndConfigurable()
    {
        var premierTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "admin@premier.com", "TenantAdmin123!", $"privacy-{suffix}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var initial = await client.GetAsync("/api/privacy/me");
        initial.StatusCode.Should().Be(HttpStatusCode.OK, await initial.Content.ReadAsStringAsync());
        var initialPayload = await initial.Content.ReadFromJsonAsync<JsonElement>();
        var adminUserId = initialPayload.GetProperty("profile").GetProperty("id").GetGuid();
        var originalFirstName = initialPayload.GetProperty("profile").GetProperty("firstName").GetString()!;
        var originalLastName = initialPayload.GetProperty("profile").GetProperty("lastName").GetString()!;

        var consent = await client.PutAsJsonAsync("/api/privacy/consents/Analytics", new
        {
            granted = true,
            policyVersion = $"acceptance-{suffix}",
            reason = "Analytics acceptance verified."
        });
        consent.StatusCode.Should().Be(HttpStatusCode.OK, await consent.Content.ReadAsStringAsync());

        var settings = await client.PutAsJsonAsync("/api/privacy/settings", new
        {
            profileVisibility = (int)ProfileVisibilityLevel.TenantOnly,
            showActivity = false,
            allowAnalytics = true,
            allowMarketing = false,
            reason = "Visibility acceptance verified."
        });
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        var correction = await client.PutAsJsonAsync("/api/privacy/profile", new
        {
            firstName = "Premier",
            lastName = $"Privacy {suffix}",
            phoneNumber = "+254700000001",
            reason = "Correct inaccurate profile data."
        });
        correction.StatusCode.Should().Be(HttpStatusCode.OK, await correction.Content.ReadAsStringAsync());

        var access = await client.PostAsJsonAsync("/api/privacy/requests", new
        {
            kind = (int)PrivacyRequestKind.DataAccess,
            details = $"Credential-safe access package {suffix}"
        });
        access.StatusCode.Should().Be(HttpStatusCode.Created, await access.Content.ReadAsStringAsync());
        var accessPayload = await access.Content.ReadFromJsonAsync<JsonElement>();
        accessPayload.GetProperty("status").GetInt32().Should().Be((int)PrivacyRequestState.Completed);
        accessPayload.GetProperty("downloadAvailable").GetBoolean().Should().BeTrue();
        var accessId = accessPayload.GetProperty("id").GetGuid();
        var download = await client.GetAsync($"/api/privacy/requests/{accessId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        var export = await download.Content.ReadAsStringAsync();
        export.Should().Contain("admin@premier.com");
        export.ToLowerInvariant().Should().NotContain("passwordhash");
        export.ToLowerInvariant().Should().NotContain("refreshtoken");

        var deletion = await client.PostAsJsonAsync("/api/privacy/requests", new
        {
            kind = (int)PrivacyRequestKind.AccountDeletion,
            details = $"Deletion request acceptance {suffix}"
        });
        deletion.StatusCode.Should().Be(HttpStatusCode.Created, await deletion.Content.ReadAsStringAsync());
        var deletionId = (await deletion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var queue = await client.GetAsync($"/api/privacy/admin/requests?tenantId={premierTenantId}");
        queue.StatusCode.Should().Be(HttpStatusCode.OK, await queue.Content.ReadAsStringAsync());
        (await queue.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Should().Contain(item => item.GetProperty("request").GetProperty("id").GetGuid() == deletionId);
        (await client.GetAsync($"/api/privacy/admin/requests?tenantId={nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reject = await client.PutAsJsonAsync($"/api/privacy/admin/requests/{deletionId}/decision", new
        {
            approved = false,
            response = "Deletion was rejected during non-destructive acceptance validation."
        });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());

        var retention = await client.PostAsJsonAsync("/api/privacy/admin/retention", new
        {
            tenantId = premierTenantId,
            name = $"Privacy request retention {suffix}",
            resource = (int)RetentionResource.PrivacyRequests,
            retentionDays = 3650,
            action = (int)RetentionAction.Anonymize,
            runIntervalHours = 24,
            isActive = true,
            reason = "Retention acceptance configuration."
        });
        retention.StatusCode.Should().Be(HttpStatusCode.Created, await retention.Content.ReadAsStringAsync());
        var policyId = (await retention.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var run = await client.PostAsJsonAsync($"/api/privacy/admin/retention/{policyId}/run", new
        {
            reason = "Retention acceptance execution."
        });
        run.StatusCode.Should().Be(HttpStatusCode.OK, await run.Content.ReadAsStringAsync());
        (await run.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("affected").GetInt32().Should().Be(0);

        var cleanup = await client.DeleteAsync($"/api/privacy/admin/retention/{policyId}?reason={Uri.EscapeDataString("Acceptance policy cleanup.")}");
        cleanup.StatusCode.Should().Be(HttpStatusCode.NoContent, await cleanup.Content.ReadAsStringAsync());
        var restore = await client.PutAsJsonAsync("/api/privacy/profile", new
        {
            firstName = originalFirstName,
            lastName = originalLastName,
            phoneNumber = (string?)null,
            reason = "Restore seeded profile after acceptance validation."
        });
        restore.StatusCode.Should().Be(HttpStatusCode.OK, await restore.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        (await database.ConsentRecords.CountAsync(item => item.UserId == adminUserId && item.Kind == ConsentKind.Analytics))
            .Should().BeGreaterThan(0);
        var actions = await database.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId == premierTenantId && item.Timestamp > DateTime.UtcNow.AddMinutes(-5))
            .Select(item => item.Action).ToArrayAsync();
        actions.Should().Contain(new[]
        {
            "PrivacyConsentGranted", "PrivacySettingsUpdated", "PrivacyDataCorrected",
            "PrivacyRequestCreated", "PrivacyDataDownloaded", "PrivacyRequestRejected",
            "PrivacyRetentionPolicyCreated", "PrivacyRetentionExecuted", "PrivacyRetentionPolicyDeleted"
        });
    }

    [Fact]
    public async Task SponsorManagement_IsDurableDateControlledConfigurableTenantScopedAndAudited()
    {
        var premierTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var nbaTenantId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        Guid premierCompetitionId;
        Guid nbaCompetitionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            premierCompetitionId = await database.Competitions.AsNoTracking()
                .Where(item => item.TenantId == premierTenantId)
                .Select(item => item.Id).FirstAsync();
            nbaCompetitionId = await database.Competitions.AsNoTracking()
                .Where(item => item.TenantId == nbaTenantId)
                .Select(item => item.Id).FirstAsync();
        }

        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client, "admin@premier.com", "TenantAdmin123!", "sponsor-management");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        async Task<Guid> CreateSponsor(string name)
        {
            var response = await client.PostAsJsonAsync("/api/sponsors", new
            {
                tenantId = premierTenantId,
                name,
                description = "Hosted sponsor acceptance",
                logoUrl = $"https://assets.example/{suffix}.svg",
                website = "https://sponsor.example",
                contactEmail = $"commercial-{suffix}@example.com",
                contactPhone = "+254700000000",
                tier = (int)SponsorTier.Gold
            });
            response.StatusCode.Should().Be(
                HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
        }

        async Task<Guid> CreatePlacement(
            Guid sponsorId,
            string name,
            DateTime startDate,
            DateTime endDate,
            string placement = SponsorPlacements.PublicHome,
            Guid? competitionId = null,
            int displayOrder = 2)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/sponsors/{sponsorId}/sponsorships", new
                {
                    type = (int)SponsorshipType.Title,
                    name,
                    description = "Configured public placement",
                    value = 25000m,
                    currency = "KES",
                    startDate,
                    endDate,
                    benefits = "Homepage brand exposure",
                    contractUrl = "https://contracts.example/agreement.pdf",
                    competitionId = competitionId ?? premierCompetitionId,
                    contestId = (Guid?)null,
                    placement,
                    displayOrder
                });
            response.StatusCode.Should().Be(
                HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
        }

        var activeName = $"Active Telecom {suffix}";
        var futureName = $"Future Bank {suffix}";
        var expiredName = $"Expired Drinks {suffix}";
        var priorityName = $"Priority Airline {suffix}";
        var activeSponsorId = await CreateSponsor(activeName);
        var futureSponsorId = await CreateSponsor(futureName);
        var expiredSponsorId = await CreateSponsor(expiredName);
        var prioritySponsorId = await CreateSponsor(priorityName);
        var now = DateTime.UtcNow;
        var activePlacementId = await CreatePlacement(
            activeSponsorId, $"Active campaign {suffix}", now.AddDays(-1), now.AddDays(1));
        var futurePlacementId = await CreatePlacement(
            futureSponsorId, $"Future campaign {suffix}", now.AddDays(2), now.AddDays(3));
        var expiredPlacementId = await CreatePlacement(
            expiredSponsorId, $"Expired campaign {suffix}", now.AddDays(-3), now.AddDays(-2));
        var priorityPlacementId = await CreatePlacement(
            prioritySponsorId, $"Priority campaign {suffix}", now.AddDays(-1), now.AddDays(1),
            displayOrder: 1);
        foreach (var id in new[]
                 {
                     activePlacementId, futurePlacementId, expiredPlacementId, priorityPlacementId
                 })
        {
            var activate = await client.PostAsync(
                $"/api/sponsors/sponsorships/{id}/activate", null);
            activate.StatusCode.Should().Be(
                HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
        }

        var ownList = await client.GetFromJsonAsync<JsonElement>(
            $"/api/sponsors?tenantId={premierTenantId}");
        ownList.GetProperty("placements").EnumerateArray()
            .Select(item => item.GetString()).Should().Contain(new[]
            {
                SponsorPlacements.PublicHome,
                SponsorPlacements.CompetitionHeader,
                SponsorPlacements.ContestHeader,
                SponsorPlacements.ContestSheet,
                SponsorPlacements.Sidebar,
                SponsorPlacements.Footer
            });
        var ownedSponsors = ownList.GetProperty("items").EnumerateArray().ToArray();
        ownedSponsors.Should().Contain(item => item.GetProperty("id").GetGuid() == activeSponsorId);
        var activeDeal = ownedSponsors.Single(item =>
                item.GetProperty("id").GetGuid() == activeSponsorId)
            .GetProperty("sponsorships").EnumerateArray().Single();
        activeDeal.GetProperty("placement").GetString().Should().Be(SponsorPlacements.PublicHome);
        activeDeal.GetProperty("displayOrder").GetInt32().Should().Be(2);
        activeDeal.GetProperty("isCurrentlyVisible").GetBoolean().Should().BeTrue();

        var publicHome = await client.GetFromJsonAsync<JsonElement>(
            "/api/public/tenants/premier-league/home");
        var publicNames = publicHome.GetProperty("sponsors").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()).ToArray();
        publicNames.Should().Contain(activeName);
        publicNames.Should().Contain(priorityName);
        publicNames.Should().NotContain(futureName);
        publicNames.Should().NotContain(expiredName);
        Array.IndexOf(publicNames, priorityName).Should().BeLessThan(
            Array.IndexOf(publicNames, activeName));

        (await client.GetAsync($"/api/sponsors?tenantId={nbaTenantId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/sponsors/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignCreate = await client.PostAsJsonAsync("/api/sponsors", new
        {
            tenantId = nbaTenantId,
            name = $"Forbidden sponsor {suffix}",
            tier = (int)SponsorTier.Standard
        });
        foreignCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var foreignAssociation = await client.PostAsJsonAsync(
            $"/api/sponsors/{activeSponsorId}/sponsorships", new
            {
                type = (int)SponsorshipType.Title,
                name = $"Foreign association {suffix}",
                value = 1m,
                currency = "USD",
                competitionId = nbaCompetitionId,
                contestId = (Guid?)null,
                placement = SponsorPlacements.PublicHome,
                displayOrder = 0
            });
        foreignAssociation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await foreignAssociation.Content.ReadAsStringAsync())
            .Should().Contain("does not belong to this tenant");

        string originalEntitlements;
        string originalUsageLimits;
        using (var entitlementScope = _factory.Services.CreateScope())
        {
            var database = entitlementScope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var subscription = await database.Subscriptions.SingleAsync(item =>
                item.TenantId == premierTenantId
                && item.Status != SubscriptionStatus.Draft
                && item.Status != SubscriptionStatus.Archived);
            originalEntitlements = subscription.FeatureEntitlements;
            originalUsageLimits = subscription.UsageLimits;
            subscription.ConfigureFeatures(
                "{\"sponsorship.placements\":false}", originalUsageLimits);
            await database.SaveChangesAsync();
        }
        var disabledPlacement = await client.PostAsJsonAsync(
            $"/api/sponsors/{activeSponsorId}/sponsorships", new
            {
                type = (int)SponsorshipType.Media,
                name = $"Disabled placement {suffix}",
                value = 1m,
                currency = "USD",
                competitionId = premierCompetitionId,
                contestId = (Guid?)null,
                placement = SponsorPlacements.Footer,
                displayOrder = 0
            });
        disabledPlacement.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await disabledPlacement.Content.ReadAsStringAsync())
            .Should().Contain("FeatureNotEntitled");
        using (var entitlementScope = _factory.Services.CreateScope())
        {
            var database = entitlementScope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var subscription = await database.Subscriptions.SingleAsync(item =>
                item.TenantId == premierTenantId
                && item.Status != SubscriptionStatus.Draft
                && item.Status != SubscriptionStatus.Archived);
            subscription.ConfigureFeatures(originalEntitlements, originalUsageLimits);
            await database.SaveChangesAsync();
        }

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider
            .GetRequiredService<SportsPlatformDbContext>();
        (await verificationDatabase.Sponsors.AsNoTracking().SingleAsync(
            item => item.Id == activeSponsorId)).TenantId.Should().Be(premierTenantId);
        (await verificationDatabase.Sponsorships.AsNoTracking().SingleAsync(
            item => item.Id == activePlacementId)).TenantId.Should().Be(premierTenantId);
        var actions = await verificationDatabase.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId == premierTenantId
                && (item.EntityId == activeSponsorId.ToString()
                    || item.EntityId == activePlacementId.ToString()))
            .Select(item => item.Action).ToArrayAsync();
        actions.Should().Contain("SponsorCreated");
        actions.Should().Contain("SponsorshipCreated");
        actions.Should().Contain("SponsorshipActive");
    }

    [Fact]
    public async Task CompetitionWizard_AssignsPublishedRulesetAndRejectsInvalidPublicationStructure()
    {
        using var client = _factory.CreateClient();
        var admin = await LoginAsync(
            client,
            "admin@sportsplatform.com",
            "Admin123!",
            "competition-wizard-validation");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        Guid sportId;
        Guid rulesetVersionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var sport = await database.Sports
                .Include(item => item.Versions)
                .OrderBy(item => item.Code)
                .FirstAsync();
            if (!await database.TenantSports.AnyAsync(item =>
                    item.TenantId == admin.TenantId && item.SportId == sport.Id))
                database.TenantSports.Add(
                    new SportsPlatform.Modules.Sports.Entities.TenantSport(
                        Guid.NewGuid(), admin.TenantId, sport.Id));
            var published = sport.Versions
                .Where(version => version.IsPublished)
                .OrderByDescending(version => version.VersionNumber)
                .FirstOrDefault();
            if (published is null)
            {
                published = sport.CreateNewVersion("Wizard published ruleset", "integration-test");
                published.ConfigureContestShape(
                    "TeamVsTeam", "Team", "[]", "[]", "[]", "[]", "[]", "{}", "{}");
                published.Publish();
                database.Add(published);
            }
            await database.SaveChangesAsync();
            sportId = sport.Id;
            rulesetVersionId = published.Id;
        }

        var create = await client.PostAsJsonAsync("/api/competitions", new
        {
            tenantId = admin.TenantId,
            name = $"Wizard validation {Guid.NewGuid():N}",
            sportId
        });
        create.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await create.Content.ReadAsStringAsync());
        var competition = await create.Content.ReadFromJsonAsync<JsonElement>();
        var competitionId = competition.GetProperty("id").GetGuid();

        (await client.PutAsync(
            $"/api/competitions/{competitionId}/ruleset/{rulesetVersionId}", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var validation = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/publication-validation");
        validation.GetProperty("isValid").GetBoolean().Should().BeFalse();
        validation.GetProperty("errors").EnumerateArray()
            .Select(item => item.GetProperty("field").GetString())
            .Should().Contain(new[] { "edition", "stages" });

        var publish = await client.PostAsync(
            $"/api/competitions/{competitionId}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await publish.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("error").GetString().Should().Be("InvalidCompetitionStructure");
    }

    private static async Task<LoginResult> LoginAsync(
        HttpClient client,
        string email,
        string password,
        string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password,
            deviceId,
            deviceName = "API integration tests",
            deviceType = "test"
        });
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new LoginResult(
            payload.RootElement.GetProperty("accessToken").GetString()!,
            payload.RootElement.GetProperty("user")
                .GetProperty("tenantId")
                .GetGuid());
    }

    private sealed record LoginResult(string AccessToken, Guid TenantId);
}
