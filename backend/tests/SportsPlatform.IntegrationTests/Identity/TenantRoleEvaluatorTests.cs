using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// The evaluator's tenant step (S03 D10): an active membership of the request's tenant and an unrevoked assignment
/// whose role grants the action, whose scope type can grant it, and whose scope is the tenant or in the resource's
/// scope chain (#45 N1). Read under the request's tenant.
/// </summary>
public sealed class TenantRoleEvaluatorTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _metroFootballLeague = new("0192a1b2-0000-7000-8000-00000000d001");
    private static readonly Guid _under17Cup = new("0192a1b2-0000-7000-8000-00000000d002");
    private static readonly Guid _ntindaSwifts = new("0192a1b2-0000-7000-8000-00000000d003");
    private static readonly Guid _metroFootballLeagueOpener = new("0192a1b2-0000-7000-8000-00000000d004");

    [Fact]
    public async Task A_tenant_wide_role_grants_its_actions_in_its_tenant_only()
    {
        var (kmsa, elssc) = await TenantsAsync();
        var viewer = await CreateMemberAsync(App.Services, kmsa, "ruth.akello@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));

        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, viewer.Actor, kmsa)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(TenancyActions.TenantSettingsUpdate, viewer.Actor, kmsa)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, viewer.Actor, elssc)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task The_owner_and_admin_change_the_settings()
    {
        var (kmsa, _) = await TenantsAsync();
        var owner = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var admin = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));

        (await AuthorizeAsync(TenancyActions.TenantSettingsUpdate, owner.Actor, kmsa)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(TenancyActions.TenantSettingsUpdate, admin.Actor, kmsa)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Suspended_removed_and_role_less_members_are_refused()
    {
        var (kmsa, _) = await TenantsAsync();
        var suspended = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var removed = await CreateMemberAsync(App.Services, kmsa, "esther.nabirye@kmsa.example", Ct, TenantWide(IdentityRoles.ResultsOfficer));
        var revoked = await CreateMemberAsync(App.Services, kmsa, "ruth.akello@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        await ChangeAsync(App.Services, suspended, m => m.Suspend("Under review", SystemActor.Instance, JoinedAt), Ct);
        await ChangeAsync(App.Services, removed, m => m.Remove("Left the association", SystemActor.Instance, JoinedAt), Ct);
        await ChangeAsync(App.Services, revoked, m => m.RevokeRole(m.Roles.Single().Id, SystemActor.Instance, JoinedAt), Ct);

        foreach (var member in new[] { suspended, removed, revoked })
        {
            (await AuthorizeAsync(TenancyActions.TenantSettingsRead, member.Actor, kmsa)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        }
    }

    [Fact]
    public async Task A_competition_scoped_role_grants_no_tenant_wide_action()
    {
        var (kmsa, _) = await TenantsAsync();
        var manager = await CreateMemberAsync(
            App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, (IdentityRoles.CompetitionManager, AssignmentScope.Competition, _metroFootballLeague));

        // COMPETITION_MANAGER includes tenant.settings.read, but the action is scopable by TENANT only.
        IdentityRoles.All.Single(r => r.Code == IdentityRoles.CompetitionManager).Grants(TenancyActions.TenantSettingsRead).ShouldBeTrue();
        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, manager.Actor, kmsa)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, manager.Actor, kmsa, new ScopeRef(AssignmentScope.Competition, _metroFootballLeague)))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task A_scoped_role_grants_on_its_resource_and_the_resources_below_it()
    {
        var (kmsa, _) = await TenantsAsync();
        var reader = await CreateMemberAsync(
            App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, (FixtureReader, AssignmentScope.Competition, _metroFootballLeague));
        var chains = new FakeScopeChain(AssignmentScope.Contest, _metroFootballLeagueOpener, new ScopeRef(AssignmentScope.Competition, _metroFootballLeague));

        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, reader.Actor, kmsa, new ScopeRef(AssignmentScope.Competition, _metroFootballLeague), chains))
            .IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, reader.Actor, kmsa, new ScopeRef(AssignmentScope.Contest, _metroFootballLeagueOpener), chains))
            .IsSuccess.ShouldBeTrue();

        // Another competition, no resource at all, or a contest without a known chain: refused.
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, reader.Actor, kmsa, new ScopeRef(AssignmentScope.Competition, _under17Cup), chains))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, reader.Actor, kmsa, scope: null, chains))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, reader.Actor, kmsa, new ScopeRef(AssignmentScope.Contest, _metroFootballLeagueOpener)))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task An_assignment_at_a_scope_type_the_action_does_not_allow_grants_nothing()
    {
        var (kmsa, _) = await TenantsAsync();
        var teamReader = await CreateMemberAsync(
            App.Services, kmsa, "patrick.opio@kmsa.example", Ct, (FixtureReader, AssignmentScope.Team, _ntindaSwifts));

        // The role includes fixture.read and the scope matches the resource, but fixture.read isn't scopable by TEAM.
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, teamReader.Actor, kmsa, new ScopeRef(AssignmentScope.Team, _ntindaSwifts)))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task A_request_without_its_tenant_set_is_refused()
    {
        var (kmsa, elssc) = await TenantsAsync();
        var viewer = await CreateMemberAsync(App.Services, kmsa, "ruth.akello@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));

        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, viewer.Actor, requestTenant: null, scopeTenant: kmsa))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, viewer.Actor, requestTenant: kmsa, scopeTenant: elssc))
            .Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task Only_users_hold_tenant_roles()
    {
        var (kmsa, _) = await TenantsAsync();

        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, SystemActor.Instance, kmsa)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, AnonymousActor.Instance, kmsa)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    private async Task<(Guid Kmsa, Guid Elssc)> TenantsAsync() => (
        await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct),
        await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct));

    private Task<Result> AuthorizeAsync(
        ActionDefinition action, ICurrentActor actor, Guid tenantId, ScopeRef? scope = null, IScopeChainProvider? chains = null) =>
        AuthorizeAsync(action, actor, tenantId, tenantId, scope, chains);

    private async Task<Result> AuthorizeAsync(
        ActionDefinition action, ICurrentActor actor, Guid? requestTenant, Guid scopeTenant, ScopeRef? scope = null, IScopeChainProvider? chains = null)
    {
        await using var serviceScope = App.Services.CreateAsyncScope();
        var services = serviceScope.ServiceProvider;
        var tenantContext = services.GetRequiredService<TenantContext>();
        tenantContext.Set(scopeTenant);

        // The application's roles, plus the test-only role that grants the test action fixture.read.
        var evaluator = new IdentityAuthorizationEvaluator(
            new Permissions(services.GetRequiredService<IdentityDbContext>(), Roles, tenantContext),
            new ScopeChains(chains is null ? [] : [chains]));
        return await evaluator.AuthorizeAsync(new AuthorizationRequest(action, actor, requestTenant, scope), Ct);
    }
}
