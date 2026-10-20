using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Identity.Features.Capabilities.GetTenantCapabilities;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>GET /api/admin/v1/me/capabilities</c> and <c>GET /api/platform/v1/me/capabilities</c> (S03-T5): the actions the
/// evaluator allows the caller, for every system role (table-driven), tenant-wide or on a resource through its chain.
/// </summary>
public sealed class CapabilitiesTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public const string TenantCapabilitiesPath = "/api/admin/v1/me/capabilities";
    public const string PlatformCapabilitiesPath = "/api/platform/v1/me/capabilities";

    private static readonly Guid _metroFootballLeague = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _under17Cup = new("0192a1b2-0000-7000-8000-00000000c002");
    private static readonly Guid _metroFootballLeagueOpener = new("0192a1b2-0000-7000-8000-00000000d001");

    /// <summary>Every tenant role that can be held tenant-wide (<c>TEAM_MANAGER</c> can't: see below).</summary>
    public static TheoryData<string> TenantWideRoles =>
        [.. IdentityRoles.All.Where(role => role.Scope == RoleScope.Tenant && role.AssignableAt.Contains(AssignmentScope.Tenant)).Select(role => role.Code)];

    public static TheoryData<string> PlatformRoleCodes => [.. PlatformRoles.All];

    [Theory]
    [MemberData(nameof(TenantWideRoles))]
    public async Task A_members_tenant_capabilities_are_what_the_evaluator_allows(string role)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, MailboxOf(role, "kmsa.example"), Ct, TenantWide(role));
        using var client = App.ClientFor(member.UserId, kmsa);

        using var response = await client.GetAsync(TenantCapabilitiesPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var expected = await EvaluatorAllowsAsync(member.Actor, ActionScope.Tenant, kmsa);
        expected.ShouldContain(IdentityActions.TenantCapabilitiesRead.Code.Value);
        ActionsOf(body).ShouldBe(expected);

        // And independently of the shared rule: the role's tenant actions that a tenant-wide assignment can grant.
        ActionsOf(body).ShouldBe(await RoleGrantsAsync(role, action => action.Scope == ActionScope.Tenant && action.ScopableBy.Contains(AssignmentScope.Tenant)));
        body.GetProperty("scope").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [MemberData(nameof(PlatformRoleCodes))]
    public async Task A_platform_users_capabilities_are_what_the_evaluator_allows(string role)
    {
        App.Authorization.UseIdentityRoles();
        var userId = await AccountTestData.CreateUserAsync(App.Services, MailboxOf(role, "msp.example"), TestPasswords.New(), Ct, platformRole: role);
        using var client = App.CreateClient().AsUser(userId);

        using var response = await client.GetAsync(PlatformCapabilitiesPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var expected = await EvaluatorAllowsAsync(new UserActor(userId), ActionScope.Platform, tenantId: null);
        expected.ShouldContain(IdentityActions.PlatformCapabilitiesRead.Code.Value);
        var actions = ActionsOf(await response.Content.ReadFromJsonAsync<JsonElement>(Ct));
        actions.ShouldBe(expected);
        actions.ShouldBe(await RoleGrantsAsync(role, action => action.Scope == ActionScope.Platform));
    }

    [Fact]
    public async Task On_a_resource_the_capabilities_add_what_its_chain_grants()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var reader = await CreateMemberAsync(
            App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct,
            TenantWide(IdentityRoles.Registrar), (FixtureReader, AssignmentScope.Competition, _metroFootballLeague));
        var chains = new FakeScopeChain(AssignmentScope.Contest, _metroFootballLeagueOpener, new ScopeRef(AssignmentScope.Competition, _metroFootballLeague));
        var tenantWide = await TenantCapabilitiesAsync(reader, kmsa, resource: null, chains);
        tenantWide.ShouldNotContain(AuthorizationTestActions.FixtureRead.Code.Value);

        // The competition, and a contest below it: the scoped role adds fixture.read, as the evaluator decides.
        foreach (var resource in new[]
        {
            new ScopeRef(AssignmentScope.Competition, _metroFootballLeague), new ScopeRef(AssignmentScope.Contest, _metroFootballLeagueOpener),
        })
        {
            var capabilities = await TenantCapabilitiesAsync(reader, kmsa, resource, chains);
            capabilities.ShouldBe([.. tenantWide.Append(AuthorizationTestActions.FixtureRead.Code.Value).Order(StringComparer.Ordinal)]);
            (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, reader, kmsa, resource, chains)).ShouldBeTrue();
        }

        // Another competition: what the tenant-wide role grants, nothing more.
        (await TenantCapabilitiesAsync(reader, kmsa, new ScopeRef(AssignmentScope.Competition, _under17Cup), chains)).ShouldBe(tenantWide);
    }

    [Fact]
    public async Task On_a_resource_over_HTTP_the_scope_is_echoed()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var viewer = await CreateMemberAsync(App.Services, kmsa, "ruth.akello@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        using var client = App.ClientFor(viewer.UserId, kmsa);

        using var response = await client.GetAsync($"{TenantCapabilitiesPath}?scopeType=COMPETITION&scopeId={_metroFootballLeague}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        ActionsOf(body).ShouldBe(await EvaluatorAllowsAsync(viewer.Actor, ActionScope.Tenant, kmsa));
        body.GetProperty("scope").GetProperty("type").GetString().ShouldBe("COMPETITION");
        body.GetProperty("scope").GetProperty("id").GetGuid().ShouldBe(_metroFootballLeague);
    }

    [Theory]
    [InlineData("scopeType=COMPETITION")]
    [InlineData("scopeId=0192a1b2-0000-7000-8000-00000000c001")]
    [InlineData("scopeType=TENANT&scopeId=0192a1b2-0000-7000-8000-00000000c001")]
    [InlineData("scopeType=competition&scopeId=0192a1b2-0000-7000-8000-00000000c001")]
    [InlineData("scopeType=COMPETITION&scopeId=metro-league")]
    [InlineData("scopeType=COMPETITION&scopeId=00000000-0000-0000-0000-000000000000")]
    public async Task A_malformed_scope_is_refused(string query)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var viewer = await CreateMemberAsync(App.Services, kmsa, "ruth.akello@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        using var client = App.ClientFor(viewer.UserId, kmsa);

        using var response = await client.GetAsync($"{TenantCapabilitiesPath}?{query}", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
    }

    [Fact]
    public async Task A_team_manager_reads_no_tenant_capabilities_until_requests_carry_resources()
    {
        // TEAM_MANAGER is assigned at a team only, and an action without a resource is granted only tenant-wide (S06 follow-up).
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var manager = await CreateMemberAsync(
            App.Services, kmsa, "patrick.opio@kmsa.example", Ct, (IdentityRoles.TeamManager, AssignmentScope.Team, Guid.CreateVersion7()));
        using var client = App.ClientFor(manager.UserId, kmsa);

        using var response = await client.GetAsync(TenantCapabilitiesPath, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task Who_may_not_read_capabilities_is_refused_as_for_any_action()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Provisioning, Ct);
        var noRoles = await CreateMemberAsync(App.Services, kmsa, "sarah.namubiru@kmsa.example", Ct);
        var viewer = await CreateMemberAsync(App.Services, elssc, "ruth.akello@elssc.example", Ct, TenantWide(IdentityRoles.Viewer));

        using (var anonymous = App.CreateClient())
        {
            anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", kmsa.ToString());
            await (await anonymous.GetAsync(TenantCapabilitiesPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
            await (await anonymous.GetAsync(PlatformCapabilitiesPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "AUTH_UNAUTHENTICATED");
        }

        using (var member = App.ClientFor(noRoles.UserId, kmsa))
        {
            await (await member.GetAsync(TenantCapabilitiesPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            await (await member.GetAsync(PlatformCapabilitiesPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }

        using (var outsider = App.ClientFor(noRoles.UserId, elssc))
        {
            await (await outsider.GetAsync(TenantCapabilitiesPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        }

        using (var provisioning = App.ClientFor(viewer.UserId, elssc))
        {
            await (await provisioning.GetAsync(TenantCapabilitiesPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_PROVISIONING");
        }
    }

    /// <summary>A role's mailbox, e.g. <c>results.officer@kmsa.example</c>.</summary>
    private static string MailboxOf(string role, string domain) => $"{role.ToLowerInvariant().Replace('_', '.')}@{domain}";

    private static List<string> ActionsOf(JsonElement body) =>
        [.. body.GetProperty("actions").EnumerateArray().Select(action => action.GetString()!)];

    /// <summary>The actions of <paramref name="scope"/> in the catalogue for which the app's evaluator answers success.</summary>
    private async Task<List<string>> EvaluatorAllowsAsync(ICurrentActor actor, ActionScope scope, Guid? tenantId)
    {
        await using var serviceScope = App.Services.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        if (tenantId is { } tenant)
        {
            services.GetRequiredService<TenantContext>().Set(tenant);
        }

        var evaluator = services.GetRequiredService<IdentityAuthorizationEvaluator>();
        var allowed = new List<string>();
        foreach (var action in services.GetRequiredService<IActionCatalogue>().All.Where(action => action.Scope == scope))
        {
            if ((await evaluator.AuthorizeAsync(new AuthorizationRequest(action, actor, tenantId), Ct)).IsSuccess)
            {
                allowed.Add(action.Code.Value);
            }
        }

        return allowed;
    }

    /// <summary>The catalogue's actions, in its ordinal order, that the role definition names and <paramref name="grantable"/> accepts.</summary>
    private async Task<List<string>> RoleGrantsAsync(string role, Func<ActionDefinition, bool> grantable)
    {
        await using var serviceScope = App.Services.CreateAsyncScope();
        var definition = IdentityRoles.All.Single(r => r.Code == role);
        return [.. serviceScope.ServiceProvider.GetRequiredService<IActionCatalogue>().All
            .Where(action => grantable(action) && definition.Actions.Contains(action.Code.Value))
            .Select(action => action.Code.Value)];
    }

    /// <summary>The handler with the test role and test actions (<c>fixture.read</c>) and a module's chain.</summary>
    private async Task<IReadOnlyList<string>> TenantCapabilitiesAsync(Member member, Guid tenantId, ScopeRef? resource, IScopeChainProvider chains)
    {
        await using var serviceScope = App.Services.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var tenantContext = services.GetRequiredService<TenantContext>();
        tenantContext.Set(tenantId);
        var handler = new GetTenantCapabilitiesHandler(
            new Permissions(services.GetRequiredService<IdentityDbContext>(), Roles, tenantContext),
            new ScopeChains([chains]),
            new TestActions(services.GetRequiredService<IActionCatalogue>()),
            member.Actor,
            tenantContext);
        return (await handler.HandleAsync(resource, Ct)).Actions;
    }

    private async Task<bool> AuthorizeAsync(ActionDefinition action, Member member, Guid tenantId, ScopeRef resource, IScopeChainProvider chains)
    {
        await using var serviceScope = App.Services.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var tenantContext = services.GetRequiredService<TenantContext>();
        tenantContext.Set(tenantId);
        var evaluator = new IdentityAuthorizationEvaluator(
            new Permissions(services.GetRequiredService<IdentityDbContext>(), Roles, tenantContext), new ScopeChains([chains]));
        return (await evaluator.AuthorizeAsync(new AuthorizationRequest(action, member.Actor, tenantId, resource), Ct)).IsSuccess;
    }

    /// <summary>The application's actions and the test action <c>fixture.read</c>.</summary>
    private sealed class TestActions(IActionCatalogue application) : IActionCatalogue
    {
        public IReadOnlyList<ActionDefinition> All { get; } =
            [.. application.All.Append(AuthorizationTestActions.FixtureRead).OrderBy(action => action.Code.Value, StringComparer.Ordinal)];

        public ActionDefinition? Find(string code) => All.SingleOrDefault(action => action.Code.Value == code);
    }
}
