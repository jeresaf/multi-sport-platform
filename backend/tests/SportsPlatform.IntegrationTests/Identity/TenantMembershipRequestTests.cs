using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Account.GetMe;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// Memberships over HTTP with the real membership check and evaluator (S03-T4a): <c>X-Tenant-Id</c> needs an active
/// membership (identity directory), actions need a role that grants them; both are read on every request, so a change
/// takes effect on the next one. <c>GET /api/account/v1/me</c> lists the user's tenants and roles.
/// </summary>
public sealed class TenantMembershipRequestTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _tenantHeader = "X-Tenant-Id";

    [Fact]
    public async Task A_member_works_in_their_tenant_with_their_roles()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var viewer = await CreateMemberAsync(App.Services, kmsa, "ruth.akello@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        using var client = ClientFor(viewer.UserId, kmsa);

        using var read = await client.GetAsync(TenantSettingsTests.SettingsPath, Ct);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var update = await client.PatchAsJsonAsync(TenantSettingsTests.SettingsPath, new { startingSoonMinutes = 45 }, Ct);
        await update.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task Removing_suspending_or_revoking_the_last_role_denies_the_next_request()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var removed = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var suspended = await CreateMemberAsync(App.Services, kmsa, "esther.nabirye@kmsa.example", Ct, TenantWide(IdentityRoles.ResultsOfficer));
        var revoked = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        using var removedClient = ClientFor(removed.UserId, kmsa);
        using var suspendedClient = ClientFor(suspended.UserId, kmsa);
        using var revokedClient = ClientFor(revoked.UserId, kmsa);
        foreach (var client in new[] { removedClient, suspendedClient, revokedClient })
        {
            (await client.GetAsync(TenantSettingsTests.SettingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await ChangeAsync(App.Services, removed, m => m.Remove("Left the association", SystemActor.Instance, JoinedAt), Ct);
        await ChangeAsync(App.Services, suspended, m => m.Suspend("Under review", SystemActor.Instance, JoinedAt), Ct);
        await ChangeAsync(App.Services, revoked, m => m.RevokeRole(m.Roles.Single().Id, SystemActor.Instance, JoinedAt), Ct);

        // Not a member any more: refused at the header. A member without roles: refused the action.
        await (await removedClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        await (await suspendedClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        await (await revokedClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");

        // Restored, the suspended member works again with the roles they kept.
        await ChangeAsync(App.Services, suspended, m => m.Restore(SystemActor.Instance, JoinedAt), Ct);
        (await suspendedClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_member_of_one_tenant_is_refused_in_another()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = ClientFor(owner.UserId, elssc);

        using var read = await client.GetAsync(TenantSettingsTests.SettingsPath, Ct);
        using var update = await client.PatchAsJsonAsync(TenantSettingsTests.SettingsPath, new { startingSoonMinutes = 45 }, Ct);

        await read.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        await update.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        (await TenantTestData.SettingsAsync(App.Services, elssc, Ct)).StartingSoonMinutes.ShouldBe(30);
    }

    [Fact]
    public async Task Me_lists_the_tenants_the_user_is_an_active_member_of_with_their_roles()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var mvule = await TenantTestData.SaveTenantAsync(App.Services, "mvule-youth", TenantStatus.Active, Ct);
        var grace = await CreateMemberAsync(
            App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner), TenantWide(IdentityRoles.Viewer));
        await JoinAsync(App.Services, elssc, grace.UserId, Ct, TenantWide(IdentityRoles.Viewer));
        var left = new Member(grace.UserId, await JoinAsync(App.Services, mvule, grace.UserId, Ct, TenantWide(IdentityRoles.Viewer)), mvule);
        await ChangeAsync(App.Services, left, m => m.Remove("Left the league", SystemActor.Instance, JoinedAt), Ct);
        await ChangeAsync(App.Services, grace, m => m.RevokeRole(m.Roles.Single(r => r.RoleCode == IdentityRoles.Viewer).Id, SystemActor.Instance, JoinedAt), Ct);
        using var client = App.CreateClient().AsUser(grace.UserId);

        using var response = await client.GetAsync(AccountTestData.MePath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = (await response.Content.ReadFromJsonAsync<MeResponse>(Ct)).ShouldNotBeNull();
        me.Tenants.OrderBy(t => t.TenantId).Select(t => (t.TenantId, string.Join(',', t.Roles))).ShouldBe(
            new[] { (kmsa, IdentityRoles.TenantOwner), (elssc, IdentityRoles.Viewer) }.OrderBy(t => t.Item1));
    }

    /// <summary>The user's client for the tenant: the membership check is the database's, not <c>X-Test-Tenants</c>.</summary>
    private HttpClient ClientFor(Guid userId, Guid tenantId)
    {
        var client = App.CreateClient().AsUser(userId);
        client.DefaultRequestHeaders.Add(_tenantHeader, tenantId.ToString());
        return client;
    }
}
