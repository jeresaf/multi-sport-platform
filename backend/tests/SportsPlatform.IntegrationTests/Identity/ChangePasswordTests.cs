using System.Net;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>POST /api/account/v1/password/change</c> (S03 D5, D15): the user's other sessions end at their next security stamp
/// check; this one stays. A wrong current password is a 400 that counts toward the lockout (owner, T2b).
/// </summary>
public sealed class ChangePasswordTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _email = "daniel.kato@kmsa.example";

    // S03 acceptance: a password change signs out the user's other sessions (security stamp).
    [Fact]
    public async Task A_change_signs_out_the_other_sessions_and_keeps_this_one()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var other = App.CreateClient();
        (await other.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();
        var newPassword = TestPasswords.New();

        using var change = await client.ChangePasswordAsync(password, newPassword, Ct);

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        change.Headers.Contains("Set-Cookie").ShouldBeTrue(); // a new cookie with the new stamp
        App.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1)); // the security stamp's check interval
        using var otherMe = await other.GetAsync(AccountTestData.MePath, Ct);
        using var thisMe = await client.GetAsync(AccountTestData.MePath, Ct);
        await otherMe.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        thisMe.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var fresh = App.CreateClient();
        (await fresh.LoginAsync(_email, password, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await fresh.LoginAsync(_email, newPassword, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "account.password.change").ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.ActorKind.ShouldBe(ActorKind.User),
            e => e.ActorId.ShouldBe(userId),
            e => e.EntityId.ShouldBe(userId));
    }

    [Fact]
    public async Task Five_wrong_current_passwords_lock_the_account()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            App.Clock.Advance(TimeSpan.FromSeconds(1)); // audit entries sort by time
            using var wrong = await client.ChangePasswordAsync(TestPasswords.New(), TestPasswords.New(), Ct);
            await wrong.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_CURRENT_PASSWORD_INVALID");
        }

        // Locked: the right password is refused too, here and at sign-in, and the session stays.
        using var locked = await client.ChangePasswordAsync(password, TestPasswords.New(), Ct);
        await locked.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_CURRENT_PASSWORD_INVALID");
        (await client.GetAsync(AccountTestData.MePath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var fresh = App.CreateClient();
        (await fresh.LoginAsync(_email, password, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "account.password.change").ToList();
        entries.Count.ShouldBe(6);
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Failure && e.ErrorCode == "AUTH_CURRENT_PASSWORD_INVALID" && e.EntityId == userId);
    }

    [Fact]
    public async Task A_short_new_password_is_rejected_naming_the_rule()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        using var response = await client.ChangePasswordAsync(password, TestPasswords.TooShort(), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_PASSWORD_REJECTED");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("12");
        problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem().GetProperty("field").GetString().ShouldBe("newPassword");
        using var fresh = App.CreateClient();
        (await fresh.LoginAsync(_email, password, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_change_needs_a_session_and_the_CSRF_header()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var anonymous = App.CreateClient();
        using var client = App.CreateClient();
        (await client.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();

        using var withoutSession = await anonymous.ChangePasswordAsync(password, TestPasswords.New(), Ct);
        using var withoutCsrf = await client.WithoutCsrfHeader().ChangePasswordAsync(password, TestPasswords.New(), Ct);

        await withoutSession.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        await withoutCsrf.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
    }
}
