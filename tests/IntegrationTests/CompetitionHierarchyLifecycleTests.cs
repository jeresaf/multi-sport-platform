using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class CompetitionHierarchyLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid TenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid SportId =
        Guid.Parse("00000000-0000-0000-0000-000000000100");
    private readonly ApiWebApplicationFactory _factory;

    public CompetitionHierarchyLifecycleTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task TenantAdministrator_CanBuildAndReadCompleteManualHierarchy()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsync(client, "admin@premier.com", "TenantAdmin123!", "competition-hierarchy");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await client.PostAsJsonAsync("/api/competitions", new
        {
            name = $"Hierarchy {suffix}", sportId = SportId, tenantId = TenantId
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var competitionId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var invalidEdition = await client.PostAsJsonAsync($"/api/competitions/{competitionId}/editions", new
        {
            name = "Invalid", startDate = "2027-06-01T00:00:00Z", endDate = "2027-01-01T00:00:00Z"
        });
        invalidEdition.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        async Task<Guid> AddEdition(string name, string start)
        {
            var response = await client.PostAsJsonAsync($"/api/competitions/{competitionId}/editions", new
            {
                name, startDate = start, endDate = (string?)null
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var editionId = await AddEdition("2026 season", "2026-01-01T00:00:00Z");
        await AddEdition("2027 season", "2027-01-01T00:00:00Z");
        var stageResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/editions/{editionId}/stages", new
            {
                name = "Group and finals", type = "GroupAndKnockout", sortOrder = 1
            });
        stageResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await stageResponse.Content.ReadAsStringAsync());
        var stageId = (await stageResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var groupResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/stages/{stageId}/groups", new { name = "Group A" });
        groupResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await groupResponse.Content.ReadAsStringAsync());
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var roundResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/stages/{stageId}/rounds", new
            {
                name = "Round 1", sortOrder = 1
            });
        roundResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await roundResponse.Content.ReadAsStringAsync());
        var roundId = (await roundResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var contestResponse = await client.PostAsJsonAsync(
            $"/api/competitions/{competitionId}/stages/{stageId}/contests", new
            {
                name = "Opening match", contestShape = "TeamVsTeam", sortOrder = 1,
                groupId, roundId, allowedParticipantTypes = new[] { "Team" }
            });
        contestResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await contestResponse.Content.ReadAsStringAsync());
        var contestId = (await contestResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var hierarchy = await client.GetFromJsonAsync<JsonElement>(
            $"/api/competitions/{competitionId}/hierarchy");
        hierarchy.GetProperty("editions").GetArrayLength().Should().Be(2);
        var stage = hierarchy.GetProperty("editions").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == editionId)
            .GetProperty("stages").EnumerateArray().Single();
        stage.GetProperty("groups").GetArrayLength().Should().Be(1);
        stage.GetProperty("rounds").GetArrayLength().Should().Be(1);
        var contest = stage.GetProperty("contests").EnumerateArray().Single();
        contest.GetProperty("id").GetGuid().Should().Be(contestId);
        contest.GetProperty("groupId").GetGuid().Should().Be(groupId);
        contest.GetProperty("roundId").GetGuid().Should().Be(roundId);

        using var otherTenant = _factory.CreateClient();
        await AuthorizeAsync(otherTenant, "scorer@nba.com", "Scorer123!", "hierarchy-isolation");
        (await otherTenant.GetAsync($"/api/competitions/{competitionId}/hierarchy"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task AuthorizeAsync(HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Competition hierarchy lifecycle test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
