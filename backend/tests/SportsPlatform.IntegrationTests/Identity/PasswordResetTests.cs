using System.Net;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>POST /api/account/v1/password/forgot</c> and <c>…/password/reset</c> (S03 D13, D15): the same answer whether or not
/// the email has an account; the email goes out through the outbox; the token lasts 1 hour, works once and only the
/// newest one works; a reset signs out every session and clears a lockout (owner, T2b).
/// </summary>
public sealed class PasswordResetTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _email = "esther.nabirye@kmsa.example";

    [Fact]
    public async Task Forgot_password_answers_the_same_for_any_email_and_mails_only_a_confirmed_user()
    {
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, TestPasswords.New(), Ct);
        await AccountTestData.CreateUserAsync(App.Services, "brian.ssemakula@kmsa.example", TestPasswords.New(), Ct, emailConfirmed: false);
        using var client = App.CreateClient();

        var answers = new List<string>();
        foreach (var email in new[] { " Esther.Nabirye@KMSA.example ", "daniel.kato@kmsa.example", "brian.ssemakula@kmsa.example" })
        {
            App.Clock.Advance(TimeSpan.FromSeconds(1)); // audit entries sort by time
            using var response = await client.ForgotPasswordAsync(email, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            answers.Add(await response.Content.ReadAsStringAsync(Ct));
        }

        answers.Distinct().ShouldHaveSingleItem();
        App.Emails.Sent.ShouldBeEmpty(); // never inside the request
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        App.Emails.Sent.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            m => m.To.ShouldBe(_email),
            m => m.Subject.ShouldBe("Reset your password"),
            m => m.TextBody.ShouldContain("http://localhost:3001/reset-password?token="));
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        entries.Select(e => (e.EntityId, Reason(e))).ShouldBe(
            [(userId, null), (null, "UNKNOWN_EMAIL"), (entries[2].EntityId, "NOT_CONFIRMED")]);
        entries.ShouldAllBe(e => e.ActionCode == "account.password.forgot" && e.Outcome == AuditOutcome.Success
            && e.ActorKind == ActorKind.Anonymous && !(e.Metadata ?? string.Empty).Contains("kmsa.example"));
    }

    [Fact]
    public async Task The_emailed_token_sets_a_new_password_once()
    {
        var oldPassword = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, oldPassword, Ct);
        using var client = App.CreateClient();
        var token = await RequestTokenAsync(client);
        var newPassword = TestPasswords.New();

        // Only the token's hash is stored.
        var stored = (await QueryAsync(DatabaseRoles.Migrator, "SELECT value FROM identity.user_tokens", r => r.GetString(0))).ShouldHaveSingleItem();
        stored.ShouldNotContain(token.Split('.')[1]);

        using var reset = await client.ResetPasswordAsync(token, newPassword, Ct);
        using var again = await client.ResetPasswordAsync(token, TestPasswords.New(), Ct);

        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        reset.Headers.Contains("Set-Cookie").ShouldBeFalse(); // a reset doesn't sign in
        await again.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_RESET_TOKEN_INVALID");
        (await client.LoginAsync(_email, oldPassword, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.LoginAsync(_email, newPassword, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await QueryAsync(DatabaseRoles.Migrator, "SELECT count(*) FROM identity.user_tokens", r => r.GetInt64(0))).ShouldBe([0L]);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "account.password.reset")
            .Select(e => (e.Outcome, e.ErrorCode, e.EntityId))
            .ShouldBe([(AuditOutcome.Success, null, userId), (AuditOutcome.Failure, "AUTH_RESET_TOKEN_INVALID", userId)]);
    }

    [Fact]
    public async Task Only_the_newest_token_works()
    {
        await AccountTestData.CreateUserAsync(App.Services, _email, TestPasswords.New(), Ct);
        using var client = App.CreateClient();
        var older = await RequestTokenAsync(client);
        var newer = await RequestTokenAsync(client);

        using var withOlder = await client.ResetPasswordAsync(older, TestPasswords.New(), Ct);
        using var withNewer = await client.ResetPasswordAsync(newer, TestPasswords.New(), Ct);

        await withOlder.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_RESET_TOKEN_INVALID");
        withNewer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_token_expires_after_one_hour()
    {
        await AccountTestData.CreateUserAsync(App.Services, _email, TestPasswords.New(), Ct);
        using var client = App.CreateClient();
        var token = await RequestTokenAsync(client);

        App.Clock.Advance(TimeSpan.FromHours(1));
        using var response = await client.ResetPasswordAsync(token, TestPasswords.New(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_RESET_TOKEN_INVALID");
    }

    [Fact]
    public async Task A_tampered_or_unknown_token_is_invalid()
    {
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, TestPasswords.New(), Ct);
        using var client = App.CreateClient();
        var token = await RequestTokenAsync(client);
        var secret = token.Split('.')[1];

        string[] tokens =
        [
            "not-a-token",
            $"{Guid.CreateVersion7():N}.{secret}", // another user's ID
            $"{userId:N}.{(secret[0] == 'A' ? 'B' : 'A')}{secret[1..]}",
        ];
        foreach (var bad in tokens)
        {
            using var response = await client.ResetPasswordAsync(bad, TestPasswords.New(), Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_RESET_TOKEN_INVALID");
        }

        (await client.ResetPasswordAsync(token, TestPasswords.New(), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_short_password_is_rejected_naming_the_rule_and_the_token_still_works()
    {
        await AccountTestData.CreateUserAsync(App.Services, _email, TestPasswords.New(), Ct);
        using var client = App.CreateClient();
        var token = await RequestTokenAsync(client);

        using var rejected = await client.ResetPasswordAsync(token, TestPasswords.TooShort(), Ct);

        var problem = await rejected.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_PASSWORD_REJECTED");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("12");
        var error = problem.GetProperty("errors").EnumerateArray().ShouldHaveSingleItem();
        (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()).ShouldBe(("newPassword", "AUTH_PASSWORD_REJECTED"));
        (await client.ResetPasswordAsync(token, TestPasswords.New(), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_reset_signs_out_every_session_and_clears_the_lockout()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var session = App.CreateClient();
        (await session.LoginAsync(_email, password, Ct)).EnsureSuccessStatusCode();
        using var client = App.CreateClient();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            (await client.LoginAsync(_email, TestPasswords.New(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var newPassword = TestPasswords.New();
        (await client.ResetPasswordAsync(await RequestTokenAsync(client), newPassword, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.LoginAsync(_email, newPassword, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        App.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1)); // the security stamp's check interval
        using var me = await session.GetAsync(AccountTestData.MePath, Ct);
        await me.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
    }

    [Fact]
    public async Task Forgot_and_reset_need_the_CSRF_header()
    {
        using var client = App.CreateClient().WithoutCsrfHeader();

        using var forgot = await client.ForgotPasswordAsync(_email, Ct);
        using var reset = await client.ResetPasswordAsync($"{Guid.CreateVersion7():N}.secret", TestPasswords.New(), Ct);

        await forgot.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        await reset.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
    }

    /// <summary>Asks for a reset email, delivers it and returns its token.</summary>
    private async Task<string> RequestTokenAsync(HttpClient client)
    {
        App.Emails.Reset();
        (await client.ForgotPasswordAsync(_email, Ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        return AccountTestData.ResetToken(App.Emails.Sent.ShouldHaveSingleItem());
    }

    private static string? Reason(AuditEntry entry) =>
        entry.Metadata is null ? null : JsonDocument.Parse(entry.Metadata).RootElement.GetProperty("reason").GetString();
}
