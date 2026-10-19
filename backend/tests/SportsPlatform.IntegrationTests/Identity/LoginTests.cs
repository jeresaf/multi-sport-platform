using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Platform.Domain.Audit;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>POST /api/account/v1/login</c> (S03 D15): lockout after five wrong passwords; an unknown email, a wrong password,
/// a locked or unconfirmed account answer the same; every attempt is audited with the real reason, never the email.
/// </summary>
public sealed class LoginTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _email = "grace.nakato@kmsa.example";

    [Fact]
    public async Task Sign_in_starts_a_session_and_is_audited_without_the_email()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();

        using var response = await client.LoginAsync(" Grace.Nakato@KMSA.example ", password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("account.login"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.ActorKind.ShouldBe(ActorKind.Anonymous),
            e => e.EntityType.ShouldBe("User"),
            e => e.EntityId.ShouldBe(userId),
            e => e.TenantId.ShouldBeNull(),
            e => (e.Metadata ?? string.Empty).ShouldNotContain("nakato", Case.Insensitive));
    }

    // S03 acceptance: lockout after 5 failures; a locked account and an unknown email answer the same.
    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_and_every_refusal_answers_the_same()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();

        var unknown = await RefusalAsync(client, "brian.ssemakula@kmsa.example", password);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            App.Clock.Advance(TimeSpan.FromSeconds(1)); // audit entries sort by time
            (await RefusalAsync(client, _email, TestPasswords.New())).ShouldBe(unknown);
        }

        App.Clock.Advance(TimeSpan.FromSeconds(1));
        var locked = await RefusalAsync(client, _email, password);

        locked.ShouldBe(unknown);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        // The fifth wrong password locks the account, so it is recorded as LOCKED_OUT.
        entries.Select(Reason).ShouldBe(
            ["UNKNOWN_EMAIL", "WRONG_PASSWORD", "WRONG_PASSWORD", "WRONG_PASSWORD", "WRONG_PASSWORD", "LOCKED_OUT", "LOCKED_OUT"]);
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Denied && e.ErrorCode == "AUTH_INVALID_CREDENTIALS"
            && e.ActorKind == ActorKind.Anonymous && !(e.Metadata ?? string.Empty).Contains("kmsa.example"));
        // Identity times lockout with the system clock (UserManager takes no TimeProvider).
        (await UserAsync()).LockoutEnd.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task A_wrong_password_is_counted_and_audited_in_the_same_save()
    {
        var password = TestPasswords.New();
        var userId = await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        using var client = App.CreateClient();

        (await RefusalAsync(client, _email, TestPasswords.New())).ShouldNotBeNull();

        (await UserAsync()).AccessFailedCount.ShouldBe(1);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Denied),
            e => e.EntityId.ShouldBe(userId),
            e => Reason(e).ShouldBe("WRONG_PASSWORD"));

        using var success = await client.LoginAsync(_email, password, Ct);
        success.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UserAsync()).AccessFailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task An_unconfirmed_account_cannot_sign_in()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct, emailConfirmed: false);
        using var client = App.CreateClient();

        (await RefusalAsync(client, _email, password)).ShouldNotBeNull();

        (await AuditTestData.EntriesAsync(App.Services, Ct)).Count.ShouldBe(0);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        Reason((await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem()).ShouldBe("NOT_ALLOWED");
    }

    // D15: no account enumeration by timing. Identity skips the password check for a locked or unconfirmed account, and
    // there is no hash for an unknown email: each refusal still hashes once.
    [Fact]
    public async Task Every_refusal_hashes_the_password_once()
    {
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, _email, password, Ct);
        await AccountTestData.CreateUserAsync(App.Services, "esther.nabirye@kmsa.example", password, Ct, emailConfirmed: false);
        using var client = App.CreateClient();

        var hashes = new List<int>
        {
            await HashesAsync(client, "brian.ssemakula@kmsa.example", password),
            await HashesAsync(client, "esther.nabirye@kmsa.example", password),
        };
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            hashes.Add(await HashesAsync(client, _email, TestPasswords.New()));
        }

        hashes.Add(await HashesAsync(client, _email, password)); // locked

        hashes.ShouldAllBe(count => count == 1);
    }

    [Theory]
    [InlineData("{}", "email")]
    [InlineData("""{"email":"grace.nakato@kmsa.example"}""", "password")]
    public async Task A_missing_field_is_a_validation_error(string body, string field)
    {
        using var client = App.CreateClient();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(AccountTestData.LoginPath, content, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        problem.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ShouldContain(field);
    }

    /// <summary>The refusal's body without its correlation ID, which differs per request.</summary>
    private static async Task<string> RefusalAsync(HttpClient client, string email, string password)
    {
        using var response = await client.LoginAsync(email, password, Ct);
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_INVALID_CREDENTIALS");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        return string.Join("|", problem.EnumerateObject()
            .Where(property => property.Name != "correlationId")
            .Select(property => $"{property.Name}={property.Value}"));
    }

    private async Task<int> HashesAsync(HttpClient client, string email, string password)
    {
        var before = App.PasswordHashes.Count;
        _ = await RefusalAsync(client, email, password);
        return App.PasswordHashes.Count - before;
    }

    private static string? Reason(AuditEntry entry) =>
        entry.Metadata is null ? null : JsonDocument.Parse(entry.Metadata).RootElement.GetProperty("reason").GetString();

    private async Task<User> UserAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(_email)).ShouldNotBeNull();
    }
}
