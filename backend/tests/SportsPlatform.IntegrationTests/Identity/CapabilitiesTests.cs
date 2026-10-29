using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
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
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>GET /api/admin/v1/me/capabilities</c> and <c>GET /api/platform/v1/me/capabilities</c> (S03-T5): the actions the
/// evaluator allows the caller, for every system role (table-driven), tenant-wide or on a resource through its chain;
/// with S04 D14, what the roles grant but the tenant can't use (<c>FULL</c>, <c>READ_ONLY</c>, without the entitlement)
/// and the tenant's access.
/// </summary>
public sealed class CapabilitiesTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public const string TenantCapabilitiesPath = "/api/admin/v1/me/capabilities";
    public const string PlatformCapabilitiesPath = "/api/platform/v1/me/capabilities";

    private static readonly Guid _metroFootballLeague = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _under17Cup = new("0192a1b2-0000-7000-8000-00000000c002");
    private static readonly Guid _metroFootballLeagueOpener = new("0192a1b2-0000-7000-8000-00000000d001");
    private const string _timeZone = "Africa/Kampala";

    // 2026-10-17 12:00 in Kampala, within the test tenants' period.
    private static readonly DateTimeOffset _entitlementTestNow = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Every tenant role that can be held tenant-wide (<c>TEAM_MANAGER</c> can't; a member without one: see below).</summary>
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

        // FULL on STANDARD: nothing the roles grant is unavailable (no action built so far names a boolean entitlement).
        var capabilities = Typed(body);
        capabilities.Unavailable.ShouldBeEmpty();
        capabilities.Access.ShouldBe(new CapabilityAccessResponse(
            TenantAccessMode.Full, SubscriptionStatus.Active, TenantTestData.PeriodEndsOn, TenantTestData.PeriodEndsOn.AddDays(14), _timeZone));
    }

    /// <summary>
    /// <c>READ_ONLY</c> (S04 D14): the list still answers; the reads stay in <c>actions</c> and the role's other grants
    /// move to <c>unavailable</c> with <c>TENANT_READ_ONLY</c>, so <c>actions</c> is exactly what steps 1 and 3 allow.
    /// </summary>
    [Theory]
    [MemberData(nameof(TenantWideRoles))]
    public async Task A_read_only_tenants_capabilities_mark_what_the_roles_grant_but_the_tenant_cant_use(string role)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(
            App.Services, kmsa, TenantTestData.PeriodStartsOn, TenantTestData.PeriodEndsOn, Ct, cancelReason: "Association stopped running competitions");
        var member = await CreateMemberAsync(App.Services, kmsa, MailboxOf(role, "kmsa.example"), Ct, TenantWide(role));
        using var client = App.ClientFor(member.UserId, kmsa);

        using var response = await client.GetAsync(TenantCapabilitiesPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var capabilities = Typed(await response.Content.ReadFromJsonAsync<JsonElement>(Ct));
        var granted = await EvaluatorAllowsAsync(member.Actor, ActionScope.Tenant, kmsa);
        var readOnly = (await ActionsAsync()).Where(action => action.AvailableWhenReadOnly).Select(action => action.Code.Value).ToHashSet();
        capabilities.Actions.ShouldBe([.. granted.Where(readOnly.Contains)]);
        capabilities.Actions.ShouldContain(IdentityActions.TenantCapabilitiesRead.Code.Value);
        capabilities.Unavailable.ShouldBe([.. granted.Where(code => !readOnly.Contains(code))
            .Select(code => new UnavailableActionResponse(code, "TENANT_READ_ONLY", Entitlement: null, Plans: null))]);
        capabilities.Access.ShouldBe(new CapabilityAccessResponse(
            TenantAccessMode.ReadOnly, SubscriptionStatus.Cancelled, TenantTestData.PeriodEndsOn, TenantTestData.PeriodEndsOn.AddDays(14), _timeZone));

        // Each one is what the API answers: a write the list marks is refused as it says.
        if (capabilities.Unavailable.Any(unavailable => unavailable.Action == IdentityActions.MembershipInvite.Code.Value))
        {
            await (await client.PostAsJsonAsync(
                "/api/admin/v1/invitations", new { email = "grace.atim@kmsa.example", roleCodes = new[] { IdentityRoles.Viewer } }, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    [Fact]
    public async Task Without_a_period_the_tenant_is_read_only_and_the_access_has_no_subscription()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        var owner = await CreateMemberAsync(App.Services, kmsa, "sarah.namubiru@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var client = App.ClientFor(owner.UserId, kmsa);

        using var response = await client.GetAsync(TenantCapabilitiesPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Typed(body).Access.ShouldBe(new CapabilityAccessResponse(TenantAccessMode.ReadOnly, SubscriptionStatus: null, EndsOn: null, GraceEndsOn: null, _timeZone));
        Typed(body).Unavailable.ShouldContain(new UnavailableActionResponse(
            IdentityActions.MembershipInvite.Code.Value, "TENANT_READ_ONLY", Entitlement: null, Plans: null));
        body.GetProperty("access").GetProperty("mode").GetString().ShouldBe("READ_ONLY");
        body.GetProperty("access").GetProperty("subscriptionStatus").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// Without the entitlement (S04 D14, D20): the test action <c>fixture.publish</c> names the test boolean
    /// <c>FIXTURE_PUBLISHING</c> (off on STANDARD, on on PRO) and a test role grants it. On STANDARD it is unavailable
    /// with <c>ENTITLEMENT_REQUIRED</c> and the plans that include it; on PRO it is in <c>actions</c>; on a read-only
    /// tenant step 1 refuses first, so the reason is <c>TENANT_READ_ONLY</c>, as the middleware answers.
    /// </summary>
    [Fact]
    public async Task An_action_whose_entitlement_the_tenant_lacks_is_unavailable_with_the_plans_that_include_it()
    {
        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, EntitlementTestApp.SavePlanValuesSql);
            await using var app = await EntitlementTestApp.StartAsync(Fixture, _entitlementTestNow, Ct);
            var kmsa = await TenantTestData.SaveTenantAsync(app.Services, "kmsa", TenantStatus.Active, Ct);
            var elssc = await TenantTestData.SaveTenantAsync(
                app.Services, "elssc", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
            await TenantTestData.SavePeriodAsync(app.Services, elssc, TenantTestData.PeriodStartsOn, TenantTestData.PeriodEndsOn, Ct, planCode: "PRO");
            var mbarara = await TenantTestData.SaveTenantAsync(
                app.Services, "mbarara", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
            await TenantTestData.SavePeriodAsync(
                app.Services, mbarara, TenantTestData.PeriodStartsOn, TenantTestData.PeriodEndsOn, Ct, cancelReason: "Association stopped running competitions");

            var onStandard = await PublisherCapabilitiesAsync(app, kmsa, "peter.okello@kmsa.example");
            onStandard.Actions.ShouldNotContain(EntitlementTestApp.FixturePublish.Code.Value);
            var missing = onStandard.Unavailable.ShouldHaveSingleItem();
            (missing.Action, missing.Code, missing.Entitlement).ShouldBe(("fixture.publish", "ENTITLEMENT_REQUIRED", "FIXTURE_PUBLISHING"));
            missing.Plans.ShouldBe(["PRO"]);

            var onPro = await PublisherCapabilitiesAsync(app, elssc, "peter.okello@elssc.example");
            onPro.Actions.ShouldContain(EntitlementTestApp.FixturePublish.Code.Value);
            onPro.Unavailable.ShouldBeEmpty();

            var readOnly = await PublisherCapabilitiesAsync(app, mbarara, "peter.okello@mbarara.example");
            readOnly.Unavailable.ShouldBe([new UnavailableActionResponse("fixture.publish", "TENANT_READ_ONLY", Entitlement: null, Plans: null)]);
        }
        finally
        {
            await ExecuteAsync(DatabaseRoles.Migrator, EntitlementTestApp.RemovePlanValuesSql);
        }
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

    /// <summary>A role held only below the tenant: <c>TEAM_MANAGER</c> (team only) and roles assigned at one resource.</summary>
    public static TheoryData<string, AssignmentScope> ResourceScopedRoles => new()
    {
        { IdentityRoles.TeamManager, AssignmentScope.Team },
        { IdentityRoles.CompetitionManager, AssignmentScope.Competition },
        { IdentityRoles.Scorer, AssignmentScope.Contest },
    };

    [Theory]
    [MemberData(nameof(ResourceScopedRoles))]
    public async Task A_member_with_no_tenant_wide_role_reads_no_tenant_capabilities_until_requests_carry_resources(string role, AssignmentScope scopeType)
    {
        // tenant.capabilities.read is granted only tenant-wide, and requests carry no resource yet, not even for the
        // member's own resource (#52 R1, S06 follow-up).
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var resourceId = Guid.CreateVersion7();
        var member = await CreateMemberAsync(App.Services, kmsa, MailboxOf(role, "kmsa.example"), Ct, (role, scopeType, resourceId));
        using var client = App.ClientFor(member.UserId, kmsa);

        using var tenantWide = await client.GetAsync(TenantCapabilitiesPath, Ct);
        using var onTheirResource = await client.GetAsync(
            $"{TenantCapabilitiesPath}?scopeType={scopeType.ToString().ToUpperInvariant()}&scopeId={resourceId}", Ct);

        await tenantWide.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await onTheirResource.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
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

    private static TenantCapabilitiesResponse Typed(JsonElement body) =>
        body.Deserialize<TenantCapabilitiesResponse>(MessageSerialization.Options).ShouldNotBeNull();

    private async Task<IReadOnlyList<ActionDefinition>> ActionsAsync()
    {
        await using var serviceScope = App.Services.CreateAsyncScope();
        return serviceScope.ServiceProvider.GetRequiredService<IActionCatalogue>().All;
    }

    /// <summary>
    /// The handler on <paramref name="app"/> (its entitlement catalogue and plan values include <c>FIXTURE_PUBLISHING</c>)
    /// for a new member holding <see cref="FixturePublisher"/> tenant-wide, with <c>fixture.publish</c> in the catalogue.
    /// </summary>
    private static async Task<TenantCapabilitiesResponse> PublisherCapabilitiesAsync(EntitlementTestApp app, Guid tenantId, string email)
    {
        var member = await CreateMemberAsync(app.Services, tenantId, email, Ct, TenantWide(FixturePublisher));
        await using var serviceScope = app.Services.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var tenantContext = services.GetRequiredService<TenantContext>();
        tenantContext.Set(tenantId);
        var handler = new GetTenantCapabilitiesHandler(
            new Permissions(services.GetRequiredService<IdentityDbContext>(), Roles, tenantContext),
            new ScopeChains([]),
            new TestActions(services.GetRequiredService<IActionCatalogue>(), EntitlementTestApp.FixturePublish),
            services.GetRequiredService<IEntitlementCatalogue>(),
            services.GetRequiredService<ITenantAccess>(),
            services.GetRequiredService<IEntitlements>(),
            member.Actor,
            tenantContext);
        return await handler.HandleAsync(resource: null, Ct);
    }

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
            services.GetRequiredService<IEntitlementCatalogue>(),
            services.GetRequiredService<ITenantAccess>(),
            services.GetRequiredService<IEntitlements>(),
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

    /// <summary>The application's actions and a test action (<c>fixture.read</c> unless another is given).</summary>
    private sealed class TestActions(IActionCatalogue application, ActionDefinition? test = null) : IActionCatalogue
    {
        public IReadOnlyList<ActionDefinition> All { get; } =
            [.. application.All.Append(test ?? AuthorizationTestActions.FixtureRead).OrderBy(action => action.Code.Value, StringComparer.Ordinal)];
    }
}
