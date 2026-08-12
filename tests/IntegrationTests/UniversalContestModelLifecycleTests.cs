using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Officials.Entities;
using SportsPlatform.Modules.Results.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class UniversalContestModelLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid TenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid SportId =
        Guid.Parse("00000000-0000-0000-0000-000000000103");
    private readonly ApiWebApplicationFactory _factory;

    public UniversalContestModelLifecycleTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CompleteModel_ConnectsParticipantsSegmentsEventsMetricsOfficialsAndResults()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsync(client);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await client.PostAsJsonAsync("/api/contests", new
        {
            tenantId = TenantId, sportId = SportId, contestShape = "HeadToHeadIndividual",
            name = $"Universal {suffix}", allowedParticipantTypes = new[] { "Athlete" }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var contestId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var participantResponse = await client.PostAsJsonAsync("/api/participants", new
        {
            tenantId = TenantId, type = "Athlete", name = $"Runner {suffix}", shortName = $"R{suffix[..3]}"
        });
        participantResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await participantResponse.Content.ReadAsStringAsync());
        var participantId = (await participantResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        Guid officialId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var userId = await database.Users.Where(item => item.TenantId == TenantId)
                .Select(item => item.Id).FirstAsync();
            officialId = Guid.NewGuid();
            database.Officials.Add(new Official(
                officialId, userId, "Universal", "Official", OfficialType.Referee));
            await database.SaveChangesAsync();
        }

        (await client.PostAsJsonAsync($"/api/contests/{contestId}/participants", new
        {
            participantId, participantType = "Athlete"
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var segmentResponse = await client.PostAsJsonAsync($"/api/contests/{contestId}/segments", new
        {
            name = "Final", segmentType = "Race", sortOrder = 1
        });
        segmentResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await segmentResponse.Content.ReadAsStringAsync());
        var segmentId = (await segmentResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/contests/{contestId}/metrics", new
        {
            name = "finishTime", numericValue = 10.42m, unit = "s", participantId, segmentId
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/contests/{contestId}/officials", new
        {
            officialId, role = "Referee", displayName = "Universal Official"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var contest = await database.Contests.Include(item => item.Events)
                .SingleAsync(item => item.Id == contestId);
            var contestEvent = contest.AddEvent(
                "FINISH", participantId, segmentId, "10.42", null,
                "test-device", 1, Guid.NewGuid().ToString("N"));
            database.Entry(contestEvent).State = EntityState.Added;
            var result = new ContestResult(Guid.NewGuid(), contestId, contestEvent.Id, participantId);
            result.SetScore(null, "10.42 s", "00:00:10.420");
            result.SetRank(1, "1");
            database.ContestResults.Add(result);
            await database.SaveChangesAsync();
        }

        var model = await client.GetFromJsonAsync<JsonElement>($"/api/contests/{contestId}/model");
        model.GetProperty("shapeKind").GetString().Should().Be("HeadToHeadIndividual");
        model.GetProperty("participants").GetArrayLength().Should().Be(1);
        model.GetProperty("segments").GetArrayLength().Should().Be(1);
        model.GetProperty("events").GetArrayLength().Should().Be(1);
        model.GetProperty("metrics").GetArrayLength().Should().Be(1);
        var metric = model.GetProperty("metrics")[0];
        metric.GetProperty("dataType").GetString().Should().Be("Decimal");
        metric.GetProperty("precision").GetInt32().Should().Be(2);
        metric.GetProperty("unit").GetString().Should().Be("s");
        model.GetProperty("officials").GetArrayLength().Should().Be(1);
        model.GetProperty("results").GetArrayLength().Should().Be(1);
    }

    private static async Task AuthorizeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@premier.com", password = "TenantAdmin123!",
            deviceId = "universal-contest-model", deviceName = "Universal contest test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
