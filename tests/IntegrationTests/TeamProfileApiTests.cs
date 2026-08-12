using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Registrations.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class TeamProfileApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly ApiWebApplicationFactory _factory;

    public TeamProfileApiTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task TeamAndClubProfiles_SupportSeasonalRosterMovementDuplicatesAndHistory()
    {
        using var owner = _factory.CreateClient();
        await AuthorizeAsync(owner, "admin@premier.com", "TenantAdmin123!", "organization-owner");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var teamCreate = await owner.PostAsJsonAsync("/api/participants/teams", new
        {
            tenantId = PremierTenantId,
            name = $"Seasonal Lions {suffix}",
            shortName = $"SL{suffix[..3]}"
        });
        teamCreate.StatusCode.Should().Be(HttpStatusCode.Created,
            await teamCreate.Content.ReadAsStringAsync());
        var teamId = (await teamCreate.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var duplicate = await owner.PostAsJsonAsync("/api/participants", new
        {
            tenantId = PremierTenantId,
            name = $"seasonal lions {suffix}",
            shortName = $"OTHER{suffix[..2]}",
            type = "Club"
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var clubCreate = await owner.PostAsJsonAsync("/api/participants", new
        {
            tenantId = PremierTenantId,
            name = $"Community Club {suffix}",
            shortName = $"CC{suffix[..3]}",
            type = "Club"
        });
        clubCreate.StatusCode.Should().Be(HttpStatusCode.Created,
            await clubCreate.Content.ReadAsStringAsync());
        var clubId = (await clubCreate.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        const string privateTeamEmail = "private.team@example.com";
        var updateTeam = await owner.PutAsJsonAsync(
            $"/api/participants/organizations/{teamId}/profile", new
            {
                name = $"Seasonal Lions {suffix}", shortName = $"SL{suffix[..3]}",
                logoUrl = "https://assets.example.test/teams/lions.png",
                homeVenue = "Lions Arena", coach = "Head Coach", manager = "Team Manager",
                contactEmail = privateTeamEmail, contactPhone = "+254700000010",
                reason = "Completed team profile"
            });
        updateTeam.StatusCode.Should().Be(HttpStatusCode.OK,
            await updateTeam.Content.ReadAsStringAsync());

        var updateClub = await owner.PutAsJsonAsync(
            $"/api/participants/organizations/{clubId}/profile", new
            {
                name = $"Community Club {suffix}", shortName = $"CC{suffix[..3]}",
                logoUrl = (string?)null, homeVenue = "Community Ground",
                coach = "Club Coach", manager = "Club Manager",
                contactEmail = "club@example.com", contactPhone = (string?)null,
                reason = "Completed club profile"
            });
        updateClub.StatusCode.Should().Be(HttpStatusCode.OK,
            await updateClub.Content.ReadAsStringAsync());
        (await updateClub.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("kind").GetString().Should().Be("Club");

        var firstRosterResponse = await owner.PostAsJsonAsync(
            $"/api/participants/teams/{teamId}/rosters", new
            {
                name = "Academy", season = "2025/26",
                startsAt = "2025-08-01T00:00:00Z", endsAt = "2026-05-31T00:00:00Z",
                competitionId = (Guid?)null, reason = "Opened academy season"
            });
        firstRosterResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await firstRosterResponse.Content.ReadAsStringAsync());
        var firstRosterId = (await firstRosterResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("rosters").EnumerateArray().Single(item =>
                item.GetProperty("season").GetString() == "2025/26").GetProperty("id").GetGuid();

        var secondRosterResponse = await owner.PostAsJsonAsync(
            $"/api/participants/teams/{teamId}/rosters", new
            {
                name = "Senior", season = "2026/27",
                startsAt = "2026-08-01T00:00:00Z", endsAt = "2027-05-31T00:00:00Z",
                competitionId = (Guid?)null, reason = "Opened senior season"
            });
        secondRosterResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await secondRosterResponse.Content.ReadAsStringAsync());
        var secondRosterId = (await secondRosterResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("rosters").EnumerateArray().Single(item =>
                item.GetProperty("season").GetString() == "2026/27").GetProperty("id").GetGuid();

        var athleteCreate = await owner.PostAsJsonAsync("/api/participants/athletes", new
        {
            tenantId = PremierTenantId,
            name = $"Moving Athlete {suffix}", shortName = $"MA{suffix[..3]}"
        });
        athleteCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var athleteId = (await athleteCreate.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var academyMove = await owner.PostAsJsonAsync(
            $"/api/participants/teams/{teamId}/rosters/{firstRosterId}/athletes/{athleteId}/transfer",
            new { jerseyNumber = "18", position = "Forward", reason = "Joined academy" });
        academyMove.StatusCode.Should().Be(HttpStatusCode.OK,
            await academyMove.Content.ReadAsStringAsync());
        var seniorMove = await owner.PostAsJsonAsync(
            $"/api/participants/teams/{teamId}/rosters/{secondRosterId}/athletes/{athleteId}/transfer",
            new { jerseyNumber = "9", position = "Forward", reason = "Promoted to senior roster" });
        seniorMove.StatusCode.Should().Be(HttpStatusCode.OK,
            await seniorMove.Content.ReadAsStringAsync());

        var registrationId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var registration = new Registration(
                registrationId, Guid.NewGuid(), teamId, RegistrationType.Team);
            registration.Submit();
            database.Registrations.Add(registration);
            await database.SaveChangesAsync();
        }

        var profile = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/participants/organizations/{teamId}/profile");
        profile.GetProperty("contactEmail").GetString().Should().Be(privateTeamEmail);
        profile.GetProperty("registrations").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetGuid() == registrationId);
        var rosters = profile.GetProperty("rosters").EnumerateArray().ToArray();
        var oldEntry = rosters.Single(item => item.GetProperty("id").GetGuid() == firstRosterId)
            .GetProperty("entries")[0];
        oldEntry.GetProperty("isActive").GetBoolean().Should().BeFalse();
        oldEntry.GetProperty("leftAt").ValueKind.Should().Be(JsonValueKind.String);
        oldEntry.GetProperty("movementReason").GetString().Should().Be("Promoted to senior roster");
        rosters.Single(item => item.GetProperty("id").GetGuid() == secondRosterId)
            .GetProperty("entries")[0].GetProperty("isActive").GetBoolean().Should().BeTrue();

        var safeJson = await (await owner.GetAsync($"/api/participants/{teamId}"))
            .Content.ReadAsStringAsync();
        safeJson.Should().NotContain(privateTeamEmail);

        using var scorer = _factory.CreateClient();
        await AuthorizeAsync(scorer, "scorer@premier.com", "Scorer123!", "organization-scorer");
        (await scorer.GetAsync($"/api/participants/organizations/{teamId}/profile"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var history = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/participants/organizations/{teamId}/history");
        var actions = history.EnumerateArray().Select(item =>
            item.GetProperty("action").GetString()).ToArray();
        actions.Should().Contain(new[]
        {
            "OrganizationProfileCreated", "OrganizationProfileUpdated",
            "TeamRosterCreated", "RosterAthleteTransferred"
        });
        history.GetRawText().Should().NotContain(privateTeamEmail);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Registrations.RemoveRange(database.Registrations.Where(item => item.Id == registrationId));
            database.Participants.RemoveRange(database.Participants.Where(item =>
                item.Id == teamId || item.Id == clubId || item.Id == athleteId));
            await database.SaveChangesAsync();
        }
    }

    private static async Task AuthorizeAsync(
        HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Team profile acceptance test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
