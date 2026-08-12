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

public sealed class CompetitionRegistrationWorkflowTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid CompetitionId = Guid.Parse("00000000-0000-0000-0000-000000000300");
    private readonly ApiWebApplicationFactory _factory;

    public CompetitionRegistrationWorkflowTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task InvalidEntry_IsBlockedUnlessAuthorizedReasonedOverrideIsRecordedAndAudited()
    {
        using var owner = _factory.CreateClient();
        var ownerId = await AuthorizeAsync(owner, "admin@premier.com", "TenantAdmin123!", "eligibility-owner");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var participantResponse = await owner.PostAsJsonAsync("/api/participants", new
        {
            tenantId = TenantId, name = $"Eligibility Athlete {suffix}", shortName = $"EA{suffix[..4]}", type = "Athlete"
        });
        participantResponse.StatusCode.Should().Be(HttpStatusCode.Created, await participantResponse.Content.ReadAsStringAsync());
        var participantId = (await participantResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var policyResponse = await owner.PutAsJsonAsync($"/api/registrations/competition/{CompetitionId}/eligibility-policy", new
        {
            minimumAge = 18, requiredDocumentTypes = new[] { "Passport" }
        });
        policyResponse.StatusCode.Should().Be(HttpStatusCode.OK, await policyResponse.Content.ReadAsStringAsync());
        var policyId = (await policyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var create = await owner.PostAsJsonAsync($"/api/registrations/competition/{CompetitionId}", new
        {
            participantId, type = RegistrationType.Individual
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var registrationId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await owner.PostAsync($"/api/registrations/{registrationId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var blocked = await owner.PostAsJsonAsync($"/api/registrations/{registrationId}/approve", new { notes = "Reviewed" });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("date of birth").And.Contain("Passport");

        const string overrideReason = "Eligibility committee granted an age and document exception";
        var approved = await owner.PostAsJsonAsync($"/api/registrations/{registrationId}/approve", new
        {
            notes = "Committee exception approved", overrideEligibility = true, overrideReason
        });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var persisted = await database.Registrations.SingleAsync(item => item.Id == registrationId);
        persisted.EligibilityOverriddenBy.Should().Be(ownerId);
        persisted.EligibilityOverrideReason.Should().Be(overrideReason);
        persisted.EligibilityIssues().Should().HaveCount(2);
        (await database.AuditEntries.AnyAsync(item => item.EntityId == registrationId.ToString()
            && item.Action == "RegistrationEligibilityOverridden" && item.Reason == overrideReason)).Should().BeTrue();

        database.Registrations.Remove(persisted);
        database.Participants.Remove(await database.Participants.SingleAsync(item => item.Id == participantId));
        database.CompetitionEligibilityPolicies.Remove(await database.CompetitionEligibilityPolicies.SingleAsync(item => item.Id == policyId));
        await database.SaveChangesAsync();
    }

    [Fact]
    public async Task Officials_RecordDecisionsEligibilityAuditsAndApplicantNotifications()
    {
        using var owner = _factory.CreateClient();
        var ownerId = await AuthorizeAsync(
            owner, "admin@premier.com", "TenantAdmin123!", "registration-workflow-owner");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var participantIds = new List<Guid>();
        var registrationIds = new List<Guid>();

        foreach (var label in new[] { "Approved", "Rejected", "Waitlisted", "Withdrawn" })
        {
            var participantResponse = await owner.PostAsJsonAsync("/api/participants", new
            {
                tenantId = TenantId,
                name = $"{label} Entry {suffix}",
                shortName = $"{label[..2].ToUpperInvariant()}{suffix[..4]}",
                type = "Team"
            });
            participantResponse.StatusCode.Should().Be(HttpStatusCode.Created,
                await participantResponse.Content.ReadAsStringAsync());
            var participantId = (await participantResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            participantIds.Add(participantId);

            var registrationResponse = await owner.PostAsJsonAsync(
                $"/api/registrations/competition/{CompetitionId}", new
                {
                    participantId,
                    type = RegistrationType.Team
                });
            registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created,
                await registrationResponse.Content.ReadAsStringAsync());
            var registrationId = (await registrationResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id").GetGuid();
            registrationIds.Add(registrationId);
            (await owner.PostAsync($"/api/registrations/{registrationId}/submit", null))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var approvedId = registrationIds[0];
        (await owner.PostAsync($"/api/registrations/{approvedId}/review", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        const string approvalReason = "Identity, documents, and competition eligibility verified";
        var approval = await owner.PostAsJsonAsync(
            $"/api/registrations/{approvedId}/approve", new { notes = approvalReason });
        approval.StatusCode.Should().Be(HttpStatusCode.OK,
            await approval.Content.ReadAsStringAsync());

        var rejectedId = registrationIds[1];
        var missingReason = await owner.PostAsJsonAsync(
            $"/api/registrations/{rejectedId}/reject", new { reason = " " });
        missingReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missingReason.Content.ReadAsStringAsync()).Should().Contain("rejection reason");

        using var scorer = _factory.CreateClient();
        await AuthorizeAsync(scorer, "scorer@premier.com", "Scorer123!", "registration-workflow-scorer");
        (await scorer.PostAsJsonAsync(
            $"/api/registrations/{rejectedId}/approve", new { notes = "Unauthorized decision" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        const string rejectionReason = "Required identity document is missing";
        (await owner.PostAsJsonAsync(
            $"/api/registrations/{rejectedId}/reject", new { reason = rejectionReason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var waitlistedId = registrationIds[2];
        const string waitlistReason = "Competition capacity has been reached";
        (await owner.PostAsJsonAsync(
            $"/api/registrations/{waitlistedId}/waitlist", new { notes = waitlistReason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var withdrawnId = registrationIds[3];
        const string withdrawalReason = "Participant requested withdrawal";
        (await owner.PostAsJsonAsync(
            $"/api/registrations/{withdrawnId}/withdraw", new { reason = withdrawalReason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var queueResponse = await owner.GetAsync(
            $"/api/registrations/competition/{CompetitionId}/review-queue");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await queueResponse.Content.ReadAsStringAsync());
        var queue = await queueResponse.Content.ReadFromJsonAsync<JsonElement>();
        var entries = queue.GetProperty("registrations").EnumerateArray()
            .Where(item => registrationIds.Contains(item.GetProperty("id").GetGuid()))
            .ToDictionary(item => item.GetProperty("id").GetGuid());
        entries[approvedId].GetProperty("status").GetString().Should().Be("Approved");
        entries[approvedId].GetProperty("eligible").GetBoolean().Should().BeTrue();
        entries[approvedId].GetProperty("reason").GetString().Should().Be(approvalReason);
        entries[approvedId].GetProperty("reviewerName").GetString().Should().NotBeNullOrWhiteSpace();
        entries[rejectedId].GetProperty("status").GetString().Should().Be("Rejected");
        entries[rejectedId].GetProperty("reason").GetString().Should().Be(rejectionReason);
        entries[waitlistedId].GetProperty("status").GetString().Should().Be("Waitlisted");
        entries[waitlistedId].GetProperty("reason").GetString().Should().Be(waitlistReason);
        entries[withdrawnId].GetProperty("status").GetString().Should().Be("Withdrawn");
        entries[withdrawnId].GetProperty("reason").GetString().Should().Be(withdrawalReason);

        var notificationsResponse = await owner.GetAsync("/api/notifications?pageSize=100");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notificationsJson = await notificationsResponse.Content.ReadAsStringAsync();
        notificationsJson.Should().Contain(approvedId.ToString());
        notificationsJson.Should().Contain(rejectedId.ToString());
        notificationsJson.Should().Contain(waitlistedId.ToString());
        notificationsJson.Should().Contain(withdrawnId.ToString());
        notificationsJson.Should().Contain("Registration approved");
        notificationsJson.Should().Contain("Registration rejected");
        notificationsJson.Should().Contain("Registration waitlisted");
        notificationsJson.Should().Contain("Registration withdrawn");

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var persisted = await database.Registrations
            .Where(item => registrationIds.Contains(item.Id)).ToArrayAsync();
        persisted.Should().OnlyContain(item => item.SubmittedBy == ownerId);
        persisted.Single(item => item.Id == approvedId).ReviewedBy.Should().Be(ownerId);
        (await database.AuditEntries.CountAsync(item =>
            registrationIds.Select(id => id.ToString()).Contains(item.EntityId)
            && item.Action.StartsWith("Registration"))).Should().BeGreaterThanOrEqualTo(12);
        (await database.AuditEntries.SingleAsync(item =>
            item.EntityId == approvedId.ToString() && item.Action == "RegistrationApproved"))
            .Reason.Should().Be(approvalReason);

        var ownedNotifications = await database.Notifications
            .Where(item => item.UserId == ownerId).ToArrayAsync();
        database.Notifications.RemoveRange(ownedNotifications.Where(item =>
            item.Metadata != null
            && registrationIds.Any(id => item.Metadata.Contains(id.ToString()))));
        database.Registrations.RemoveRange(persisted);
        database.Participants.RemoveRange(database.Participants.Where(item => participantIds.Contains(item.Id)));
        await database.SaveChangesAsync();
    }

    private static async Task<Guid> AuthorizeAsync(
        HttpClient client, string email, string password, string deviceId)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password, deviceId, deviceName = "Competition registration workflow test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.GetProperty("accessToken").GetString());
        return payload.GetProperty("user").GetProperty("id").GetGuid();
    }
}
