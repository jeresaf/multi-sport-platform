using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.Host.Administration;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// Platform roles over HTTP with Identity's evaluator (S03-T3): a platform admin created by the CLI signs in and runs
/// the S02 platform API end to end; support reads but can't change; a user without a platform role is refused.
/// </summary>
public sealed class PlatformRoleRequestTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _adminEmail = "asha.mutesi@msp.example";
    private const string _auditPath = "/api/platform/v1/audit-entries";
    private const string _outboxPath = "/api/platform/v1/diagnostics/outbox";

    [Fact]
    public async Task A_platform_admin_from_the_CLI_runs_the_platform_API()
    {
        App.Authorization.UseIdentityRoles();
        var password = TestPasswords.New();
        await CreatePlatformAdminAsync(password);
        using var client = App.CreateClient();
        (await client.LoginAsync(_adminEmail, password, Ct)).EnsureSuccessStatusCode();
        var adminId = (await (await client.GetAsync(AccountTestData.MePath, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("id").GetString();

        using var created = await client.PostAsJsonAsync(TenantTestData.TenantsPath, TenantTestData.CreateBody(), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var read = await client.GetAsync($"{TenantTestData.TenantsPath}/{tenantId}", Ct);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var activated = await client.PostAsync($"{TenantTestData.TenantsPath}/{tenantId}/activate", null, Ct);
        activated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var outbox = await client.GetAsync(_outboxPath, Ct);
        outbox.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        using var audit = await client.GetAsync($"{_auditPath}?action=tenant.create", Ct);
        audit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entry = (await audit.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("actorKind").GetString().ShouldBe("USER");
        entry.GetProperty("actorId").GetString().ShouldBe(adminId);
    }

    [Fact]
    public async Task Platform_support_reads_but_cannot_change()
    {
        App.Authorization.UseIdentityRoles();
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, "joel.okello@msp.example", password, Ct, platformRole: PlatformRoles.Support);
        using var client = App.CreateClient();
        (await client.LoginAsync("joel.okello@msp.example", password, Ct)).EnsureSuccessStatusCode();

        (await client.GetAsync(TenantTestData.TenantsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(_auditPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var create = await client.PostAsJsonAsync(TenantTestData.TenantsPath, TenantTestData.CreateBody(), Ct);
        await create.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task A_user_without_a_platform_role_is_forbidden()
    {
        App.Authorization.UseIdentityRoles();
        var password = TestPasswords.New();
        await AccountTestData.CreateUserAsync(App.Services, "ruth.akello@msp.example", password, Ct);
        using var client = App.CreateClient();
        (await client.LoginAsync("ruth.akello@msp.example", password, Ct)).EnsureSuccessStatusCode();

        using var response = await client.GetAsync(TenantTestData.TenantsPath, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    private async Task CreatePlatformAdminAsync(string password)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CreatePlatformAdminCommand.RunAsync(
            App.Services,
            ["--email", _adminEmail],
            name => name == CreatePlatformAdminCommand.PasswordVariable ? password : null,
            output,
            error,
            Ct);
        exitCode.ShouldBe(0, error.ToString());
    }
}
