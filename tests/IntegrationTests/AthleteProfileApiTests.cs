using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class AthleteProfileApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid AthleticsSportId =
        Guid.Parse("00000000-0000-0000-0000-000000000103");
    private readonly ApiWebApplicationFactory _factory;

    public AthleteProfileApiTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AthleteProfiles_AreReusableSensitivePermissionProtectedAndAudited()
    {
        using var owner = _factory.CreateClient();
        await AuthorizeAsync(owner, "admin@premier.com", "TenantAdmin123!", "athlete-owner");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await owner.PostAsJsonAsync("/api/participants/athletes", new
        {
            tenantId = PremierTenantId,
            name = $"Reusable Athlete {suffix}",
            shortName = $"RA{suffix[..3]}"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created,
            await create.Content.ReadAsStringAsync());
        var athleteId = (await create.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        const string privateEmail = "private.athlete@example.com";
        const string documentNumber = "P-PRIVATE-12345";
        var update = await owner.PutAsJsonAsync($"/api/participants/athletes/{athleteId}/profile", new
        {
            name = $"Reusable Athlete {suffix}",
            shortName = $"RA{suffix[..3]}",
            dateOfBirth = "2001-06-15T00:00:00Z",
            gender = "Female",
            competitionCategory = "Senior Women",
            nationality = "Kenyan",
            photoUrl = "https://assets.example.test/athletes/profile.webp",
            contactEmail = privateEmail,
            contactPhone = "+254700000001",
            emergencyContactName = "Emergency Guardian",
            emergencyContactRelationship = "Parent",
            emergencyContactPhone = "+254700000002",
            medicalClearanceStatus = "Cleared",
            medicalClearanceExpiresAt = DateTime.UtcNow.AddMonths(6),
            identificationDocuments = new[]
            {
                new
                {
                    type = "Passport", number = documentNumber,
                    issuingCountry = "Kenya", expiresAt = DateTime.UtcNow.AddYears(3),
                    fileUrl = "https://assets.example.test/documents/passport.pdf"
                }
            },
            reason = "Completed athlete onboarding"
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK,
            await update.Content.ReadAsStringAsync());
        var profile = await update.Content.ReadFromJsonAsync<JsonElement>();
        profile.GetProperty("contactEmail").GetString().Should().Be(privateEmail);
        profile.GetProperty("medicalClearance").GetBoolean().Should().BeTrue();
        profile.GetProperty("identificationDocuments")[0].GetProperty("number")
            .GetString().Should().Be(documentNumber);

        var register = await owner.PostAsync(
            $"/api/participants/athletes/{athleteId}/sports/{AthleticsSportId}", null);
        register.StatusCode.Should().Be(HttpStatusCode.NoContent,
            await register.Content.ReadAsStringAsync());
        var reloaded = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/participants/athletes/{athleteId}/profile");
        reloaded.GetProperty("sportIds").EnumerateArray()
            .Select(item => item.GetGuid()).Should().Contain(AthleticsSportId);

        var safe = await owner.GetAsync($"/api/participants/{athleteId}");
        safe.StatusCode.Should().Be(HttpStatusCode.OK);
        var safeJson = await safe.Content.ReadAsStringAsync();
        safeJson.Should().NotContain(privateEmail);
        safeJson.Should().NotContain(documentNumber);
        safeJson.Should().NotContain("medicalClearanceStatus");

        using var scorer = _factory.CreateClient();
        await AuthorizeAsync(scorer, "scorer@premier.com", "Scorer123!", "athlete-scorer");
        (await scorer.GetAsync($"/api/participants/{athleteId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await scorer.GetAsync($"/api/participants/athletes/{athleteId}/profile"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var foreign = _factory.CreateClient();
        await AuthorizeAsync(foreign, "scorer@nba.com", "Scorer123!", "athlete-foreign");
        (await foreign.GetAsync($"/api/participants/athletes/{athleteId}/profile"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var firstContestId = Guid.NewGuid();
        var secondContestId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            foreach (var contestId in new[] { firstContestId, secondContestId })
            {
                var contest = new Contest(contestId, PremierTenantId, AthleticsSportId, "Race");
                contest.SetName($"Reusable athlete contest {contestId:N}");
                contest.AddParticipant(athleteId, "Athlete");
                database.Contests.Add(contest);
            }
            await database.SaveChangesAsync();
            (await database.Contests.CountAsync(item =>
                (item.Id == firstContestId || item.Id == secondContestId)
                && item.Participants.Any(entry => entry.ParticipantId == athleteId)))
                .Should().Be(2);
        }

        var history = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/participants/athletes/{athleteId}/history");
        var actions = history.EnumerateArray().Select(item =>
            item.GetProperty("action").GetString()).ToArray();
        actions.Should().Contain(new[]
        {
            "AthleteProfileCreated", "AthleteProfileUpdated", "AthleteSportRegistered"
        });
        history.GetRawText().Should().NotContain(privateEmail);
        history.GetRawText().Should().NotContain(documentNumber);

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Contests.RemoveRange(database.Contests.Where(item =>
                item.Id == firstContestId || item.Id == secondContestId));
            var athlete = await database.Participants.SingleAsync(item => item.Id == athleteId);
            database.Participants.Remove(athlete);
            await database.SaveChangesAsync();
        }
    }

    private static async Task AuthorizeAsync(
        HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Athlete profile acceptance test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
