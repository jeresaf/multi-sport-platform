using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Identity.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class IdentityLifecycleTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public IdentityLifecycleTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RecoveryVerificationAndDeviceSessions_ArePurposeSeparatedAndRevocable()
    {
        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"identity-{suffix}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "OriginalPassword123!",
            firstName = "Identity",
            lastName = "Lifecycle",
            phoneNumber = (string?)null
        });
        registration.StatusCode.Should().Be(HttpStatusCode.OK, await registration.Content.ReadAsStringAsync());
        var registered = await ReadAuthAsync(registration);

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        forgot.StatusCode.Should().Be(HttpStatusCode.OK);
        var resetMessage = _factory.IdentityMessages.Single(email, "PasswordReset");

        var refreshWhileResetPending = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = registered.RefreshToken
        });
        refreshWhileResetPending.StatusCode.Should().Be(HttpStatusCode.OK,
            await refreshWhileResetPending.Content.ReadAsStringAsync());
        var rotatedBeforeReset = await ReadAuthAsync(refreshWhileResetPending);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            email,
            token = resetMessage.Token,
            newPassword = "ReplacementPassword123!"
        });
        reset.StatusCode.Should().Be(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = rotatedBeforeReset.RefreshToken
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "ReplacementPassword123!",
            deviceId = "identity-test-device",
            deviceName = "Identity test device",
            deviceType = "test"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        var authenticated = await ReadAuthAsync(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authenticated.AccessToken);

        var sessions = await client.GetAsync("/api/auth/sessions");
        sessions.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessionRows = await sessions.Content.ReadFromJsonAsync<JsonElement>();
        var activeDevice = sessionRows.EnumerateArray().Single(row =>
            row.GetProperty("deviceId").GetString() == "identity-test-device");
        activeDevice.GetProperty("isRevoked").GetBoolean().Should().BeFalse();
        (await sessions.Content.ReadAsStringAsync()).Should().NotContain(authenticated.RefreshToken);

        var verificationRequest = await client.PostAsync(
            "/api/auth/request-email-verification", null);
        verificationRequest.StatusCode.Should().Be(HttpStatusCode.OK,
            await verificationRequest.Content.ReadAsStringAsync());
        var verificationMessage = _factory.IdentityMessages.Single(email, "EmailVerification");
        client.DefaultRequestHeaders.Authorization = null;
        var verification = await client.PostAsJsonAsync("/api/auth/verify-email", new
        {
            token = verificationMessage.Token
        });
        verification.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshAfterVerification = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = authenticated.RefreshToken,
            deviceId = "identity-test-device"
        });
        refreshAfterVerification.StatusCode.Should().Be(HttpStatusCode.OK,
            await refreshAfterVerification.Content.ReadAsStringAsync());
        var finalAuth = await ReadAuthAsync(refreshAfterVerification);
        finalAuth.User.GetProperty("emailVerified").GetBoolean().Should().BeTrue();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", finalAuth.AccessToken);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = finalAuth.RefreshToken,
            deviceId = "identity-test-device"
        });
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = null;
        (await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = finalAuth.RefreshToken,
            deviceId = "identity-test-device"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var user = await database.Users.SingleAsync(item => item.Email == email);
        BCrypt.Net.BCrypt.Verify("ReplacementPassword123!", user.PasswordHash).Should().BeTrue();
        user.PasswordHash.Should().StartWith("$2");
        var tokens = await database.IdentityActionTokens
            .Where(token => token.UserId == user.Id)
            .ToArrayAsync();
        tokens.Should().HaveCount(2);
        tokens.Should().OnlyContain(token => token.ConsumedAt.HasValue);
        tokens.Should().OnlyContain(token => token.TokenHash.Length == 64);
        tokens.Select(token => token.TokenHash).Should().NotContain(resetMessage.Token);
        tokens.Select(token => token.TokenHash).Should().NotContain(verificationMessage.Token);
        (await database.AuditEntries.Where(entry => entry.UserId == user.Id)
            .Select(entry => entry.Action).ToArrayAsync())
            .Should().Contain(new[]
            {
                "PasswordResetRequested",
                "PasswordResetCompleted",
                "EmailVerificationRequested",
                "EmailVerified",
                "UserLogout"
            });
    }

    [Fact]
    public async Task TenantAdministrator_CanSuspendAndUnsuspendUser_AndSuspensionRevokesSessions()
    {
        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"suspend-{suffix}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "SuspendTarget123!",
            firstName = "Suspend",
            lastName = "Target",
            tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002")
        });
        registration.StatusCode.Should().Be(HttpStatusCode.OK);
        var target = await ReadAuthAsync(registration);

        var adminLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@premier.com",
            password = "TenantAdmin123!",
            deviceId = $"suspension-admin-{suffix}",
            deviceName = "Suspension test"
        });
        adminLogin.StatusCode.Should().Be(HttpStatusCode.OK, await adminLogin.Content.ReadAsStringAsync());
        var admin = await ReadAuthAsync(adminLogin);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var suspend = await client.PostAsJsonAsync(
            $"/api/auth/users/{target.User.GetProperty("id").GetGuid()}/suspend",
            new { reason = "Focused identity lifecycle verification" });
        suspend.StatusCode.Should().Be(HttpStatusCode.OK, await suspend.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", target.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = null;
        (await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = target.RefreshToken
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "SuspendTarget123!"
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        var unsuspend = await client.PostAsJsonAsync(
            $"/api/auth/users/{target.User.GetProperty("id").GetGuid()}/unsuspend",
            new { reason = "Focused identity lifecycle verification complete" });
        unsuspend.StatusCode.Should().Be(HttpStatusCode.OK, await unsuspend.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = null;
        (await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "SuspendTarget123!"
        })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<AuthEnvelope> ReadAuthAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new AuthEnvelope(
            json.GetProperty("accessToken").GetString()!,
            json.GetProperty("refreshToken").GetString()!,
            json.GetProperty("user"));
    }

    private sealed record AuthEnvelope(string AccessToken, string RefreshToken, JsonElement User);
}
