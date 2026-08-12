using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Identity.Services;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.IntegrationTests;

internal static class TenantIsolationMatrix
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid NbaTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");

    public static async Task AssertAsync(ApiWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        Guid nbaCompetitionId;
        Guid nbaContestId;
        Guid nbaParticipantId;
        Guid premierContestId;
        Guid nbaRegistrationId;
        Guid nbaRegistrationFormId;
        Guid nbaSubscriptionId;
        Guid nbaRoleId;
        Guid premierRoleId;
        Guid nbaUserId;
        string accessToken;

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            nbaCompetitionId = await database.Competitions
                .Where(item => item.TenantId == NbaTenantId)
                .Select(item => item.Id)
                .FirstAsync();
            nbaContestId = await database.Contests
                .Where(item => item.TenantId == NbaTenantId)
                .Select(item => item.Id)
                .FirstAsync();
            nbaParticipantId = await database.Participants
                .Where(item => item.TenantId == NbaTenantId)
                .Select(item => item.Id)
                .FirstAsync();
            premierContestId = await database.Contests
                .Where(item => item.TenantId == PremierTenantId)
                .Select(item => item.Id)
                .FirstAsync();

            var nbaRegistration = new Registration(
                Guid.NewGuid(),
                nbaContestId,
                nbaParticipantId,
                RegistrationType.Team);
            var nbaRegistrationForm = new RegistrationForm(
                Guid.NewGuid(),
                NbaTenantId,
                "NBA tenant matrix form",
                "tenant-matrix",
                nbaCompetitionId);
            database.AddRange(
                nbaRegistration,
                nbaRegistrationForm);
            await database.SaveChangesAsync();
            nbaRegistrationId = nbaRegistration.Id;
            nbaRegistrationFormId = nbaRegistrationForm.Id;
            nbaSubscriptionId = await database.Subscriptions
                .Where(subscription => subscription.TenantId == NbaTenantId)
                .Select(subscription => subscription.Id)
                .SingleAsync();
            var premierRole = new Role(
                Guid.NewGuid(),
                "Premier tenant matrix role",
                null,
                tenantId: PremierTenantId);
            var nbaRole = new Role(
                Guid.NewGuid(),
                "NBA tenant matrix role",
                null,
                tenantId: NbaTenantId);
            database.Roles.AddRange(premierRole, nbaRole);
            await database.SaveChangesAsync();
            premierRoleId = premierRole.Id;
            nbaRoleId = nbaRole.Id;
            nbaUserId = await database.Users
                .Where(user => user.TenantId == NbaTenantId)
                .Select(user => user.Id)
                .FirstAsync();

            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var caller = new User(
                Guid.NewGuid(),
                "tenant-matrix@premier.test",
                "Tenant",
                "Matrix",
                "TenantMatrix123!",
                PremierTenantId);
            database.Users.Add(caller);
            await database.SaveChangesAsync();
            (accessToken, _) = tokenService.GenerateAccessToken(
                caller,
                Array.Empty<string>(),
                new[]
                {
                    "competitions.read",
                    "contests.read",
                    "participants.read",
                    "branding.read",
                    "branding.update",
                    "subscriptions.read",
                    "subscriptions.create",
                    "subscriptions.manage",
                    "tenants.read",
                    "tenants.update",
                    "tenants.manage",
                    "roles.read",
                    "roles.update",
                    "roles.delete",
                    "roles.assign",
                    "users.read"
                });
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var ownTenantRoutes = new[]
        {
            $"/api/competitions?tenantId={PremierTenantId}",
            $"/api/contests?tenantId={PremierTenantId}",
            $"/api/participants?tenantId={PremierTenantId}",
            $"/api/branding/tenant/{PremierTenantId}",
            $"/api/subscriptions/history/{PremierTenantId}",
            $"/api/registrationforms/tenant/{PremierTenantId}",
            "/api/roles",
            "/api/auth/users"
        };
        foreach (var route in ownTenantRoutes)
        {
            var response = await client.GetAsync(route);
            response.StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.OK, HttpStatusCode.NotFound },
                $"the caller owns route {route}");
        }

        var otherTenantRoutes = new[]
        {
            $"/api/competitions?tenantId={NbaTenantId}",
            $"/api/contests?tenantId={NbaTenantId}",
            $"/api/participants?tenantId={NbaTenantId}",
            $"/api/branding/tenant/{NbaTenantId}",
            $"/api/subscriptions/history/{NbaTenantId}",
            $"/api/registrationforms/tenant/{NbaTenantId}",
            $"/api/registrationforms/competition/{nbaCompetitionId}",
            $"/api/registrations/contest/{nbaContestId}",
            $"/api/registrations/participant/{nbaParticipantId}",
            $"/api/contests/{nbaContestId}",
            $"/api/participants/{nbaParticipantId}",
            $"/api/tenants/{NbaTenantId}",
            $"/api/tenants/{NbaTenantId}/contacts",
            $"/api/roles/{nbaRoleId}"
        };
        foreach (var route in otherTenantRoutes)
        {
            var response = await client.GetAsync(route);
            response.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                $"route {route} belongs to another tenant");
        }

        var crossTenantWrites = new[]
        {
            await client.PutAsJsonAsync(
                $"/api/branding/tenant/{NbaTenantId}",
                new { tenantName = "Not allowed" }),
            await client.PostAsJsonAsync(
                "/api/subscriptions",
                new
                {
                    tenantId = NbaTenantId,
                    plan = "NotAllowed",
                    startDate = DateTime.UtcNow,
                    endDate = DateTime.UtcNow.AddDays(30)
                }),
            await client.PostAsync(
                $"/api/subscriptions/{nbaSubscriptionId}/activate",
                null),
            await client.PostAsJsonAsync(
                "/api/registrationforms",
                new
                {
                    tenantId = NbaTenantId,
                    name = "Not allowed",
                    createdBy = "tenant-matrix",
                    competitionId = nbaCompetitionId
                }),
            await client.PutAsJsonAsync(
                $"/api/registrationforms/{nbaRegistrationFormId}",
                new { name = "Not allowed" }),
            await client.PostAsJsonAsync(
                "/api/registrations",
                new
                {
                    contestId = premierContestId,
                    participantId = nbaParticipantId,
                    type = RegistrationType.Team
                }),
            await client.PostAsync(
                $"/api/registrations/{nbaRegistrationId}/submit",
                null),
            await client.PutAsJsonAsync(
                $"/api/tenants/{NbaTenantId}",
                new
                {
                    name = "Not allowed",
                    shortName = "NO",
                    description = "Cross-tenant update"
                }),
            await client.PutAsJsonAsync(
                $"/api/tenants/{NbaTenantId}/localization",
                new { timezone = "UTC" }),
            await client.PutAsJsonAsync(
                $"/api/roles/{nbaRoleId}",
                new
                {
                    name = "Not allowed",
                    permissions = Array.Empty<string>()
                }),
            await client.PostAsJsonAsync(
                "/api/roles/assign",
                new { userId = nbaUserId, roleId = premierRoleId })
        };
        crossTenantWrites.Should().OnlyContain(
            response => response.StatusCode == HttpStatusCode.Forbidden);

        var tenants = await client.GetFromJsonAsync<List<TenantSummary>>(
            "/api/tenants");
        tenants.Should().ContainSingle(tenant => tenant.Id == PremierTenantId);
    }

    private sealed record TenantSummary(Guid Id);
}
