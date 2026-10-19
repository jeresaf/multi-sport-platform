using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// The session (S03 D5): Identity's cookie, the only scheme; 12 hours sliding, 7 days absolute; only the user ID and the
/// security stamp in it. <c>GET /api/account/v1/me</c> and <c>POST …/logout</c>.
/// </summary>
public sealed class SessionTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _email = "ruth.akello@msp.example";

    [Fact]
    public async Task Session_cookie_is_host_only_secure_http_only_and_strict()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();

        using var response = await client.LoginAsync(_email, password, Ct);

        var cookie = response.Headers.GetValues("Set-Cookie").ShouldHaveSingleItem();
        cookie.ShouldStartWith("__Host-msp_session=");
        var attributes = cookie.Split(';', StringSplitOptions.TrimEntries).Skip(1).Select(a => a.ToLowerInvariant()).ToList();
        attributes.ShouldContain("path=/");
        attributes.ShouldContain("secure");
        attributes.ShouldContain("httponly");
        attributes.ShouldContain("samesite=strict");
        attributes.ShouldNotContain(a => a.StartsWith("domain=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Me_reads_the_signed_in_user_and_platform_roles_and_is_never_cached()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct, platformRole: PlatformRoles.Admin);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        using var response = await client.GetAsync(AccountTestData.MePath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        me.GetProperty("id").GetGuid().ShouldBe(userId);
        me.GetProperty("email").GetString().ShouldBe(_email);
        me.GetProperty("platformRoles").EnumerateArray().Select(r => r.GetString()).ShouldBe([PlatformRoles.Admin]);
        me.GetProperty("tenants").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Me_without_a_session_is_unauthenticated()
    {
        using var client = App.CreateClient();

        using var response = await client.GetAsync(AccountTestData.MePath, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    // S03 D4 (S02-T3a follow-up): an authenticated principal without a GUID sub is not a user.
    [Theory]
    [InlineData("not-a-user-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_principal_without_a_user_id_is_unauthenticated(string subject)
    {
        using var client = App.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthentication.UserHeader, subject);

        using var response = await client.GetAsync(AccountTestData.MePath, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    [Fact]
    public async Task Logout_ends_the_session_and_is_audited_as_the_user()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        using var logout = await client.PostAsync(AccountTestData.LogoutPath, content: null, Ct);
        using var me = await client.GetAsync(AccountTestData.MePath, Ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await me.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "account.logout").ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.ActorKind.ShouldBe(ActorKind.User),
            e => e.ActorId.ShouldBe(userId),
            e => e.EntityId.ShouldBe(userId));
    }

    [Fact]
    public async Task A_session_ends_after_12_idle_hours()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        App.Clock.Advance(TimeSpan.FromHours(11));
        using var active = await client.GetAsync(AccountTestData.MePath, Ct);
        App.Clock.Advance(TimeSpan.FromHours(12) + TimeSpan.FromMinutes(1));
        using var idle = await client.GetAsync(AccountTestData.MePath, Ct);

        active.StatusCode.ShouldBe(HttpStatusCode.OK);
        await idle.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    [Fact]
    public async Task A_session_in_use_ends_7_days_after_sign_in()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        // Used every 11 hours, so the sliding window never runs out: 15 × 11 = 165 hours.
        for (var step = 1; step <= 15; step++)
        {
            App.Clock.Advance(TimeSpan.FromHours(11));
            using var active = await client.GetAsync(AccountTestData.MePath, Ct);
            active.StatusCode.ShouldBe(HttpStatusCode.OK, $"after {step * 11} hours");
        }

        App.Clock.Advance(TimeSpan.FromHours(4));
        using var expired = await client.GetAsync(AccountTestData.MePath, Ct);

        await expired.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    [Fact]
    public async Task The_session_carries_only_the_user_id_and_the_security_stamp()
    {
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, TestPasswords.New(), Ct);
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();

        var principal = await factory.CreateAsync((await users.FindByIdAsync(userId.ToString()))!);

        principal.Claims.Select(c => c.Type).ShouldBe(["sub", "AspNet.Identity.SecurityStamp"], ignoreOrder: true);
        principal.FindFirstValue("sub").ShouldBe(userId.ToString());
        principal.Claims.ShouldNotContain(c => c.Value.Contains("akello", StringComparison.OrdinalIgnoreCase));
    }
}
