using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// The evaluator (S03 D10): a signed-in user may work on their own account, an anonymous caller only where the action
/// allows one; platform actions come from the user's unrevoked platform roles, generated over the action catalogue
/// (S03-T3 acceptance); tenant actions are refused until T4 (fail closed), platform roles never grant them.
/// </summary>
public sealed class IdentityAuthorizationEvaluatorTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly IReadOnlyList<ActionDefinition> _platformActions = [.. ActionCatalogue.Build(ModuleRegistry.All)
        .Select(entry => entry.Action)
        .Where(action => action.Scope == ActionScope.Platform)];

    [Fact]
    public async Task A_platform_admin_may_perform_every_platform_action()
    {
        var admin = await UserWithRoleAsync("asha.mutesi@msp.example", PlatformRoles.Admin);

        (await RefusedAsync(admin, _platformActions)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Platform_support_may_perform_only_the_read_actions()
    {
        var support = await UserWithRoleAsync("joel.okello@msp.example", PlatformRoles.Support);
        var reads = _platformActions.Where(action => action.Code.Value.EndsWith(".read", StringComparison.Ordinal)).ToList();

        reads.ShouldNotBeEmpty();
        (await RefusedAsync(support, reads)).ShouldBeEmpty();
        (await RefusedAsync(support, _platformActions.Except(reads))).ShouldBe(_platformActions.Except(reads).Select(a => a.Code.Value));
    }

    [Fact]
    public async Task A_user_without_a_platform_role_is_forbidden_every_platform_action()
    {
        var user = await UserWithRoleAsync("ruth.akello@msp.example", platformRole: null);

        foreach (var action in _platformActions)
        {
            (await AuthorizeAsync(action, user)).Error.ShouldBe(AuthorizationErrors.Forbidden, action.Code.Value);
        }
    }

    [Fact]
    public async Task A_revoked_platform_role_grants_nothing()
    {
        var admin = await UserWithRoleAsync("asha.mutesi@msp.example", PlatformRoles.Admin);
        await ExecuteAsync("sp_migrator",
            $"UPDATE identity.platform_role_assignments SET revoked_at = now(), revoked_by = '{Guid.Empty}' WHERE user_id = '{admin.ActorId}'");

        (await RefusedAsync(admin, _platformActions)).ShouldBe(_platformActions.Select(a => a.Code.Value));
    }

    [Fact]
    public async Task Only_users_hold_platform_roles()
    {
        (await AuthorizeAsync(TenancyActions.TenantRead, SystemActor.Instance)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(TenancyActions.TenantRead, AnonymousActor.Instance)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task Platform_roles_grant_no_tenant_actions()
    {
        var admin = await UserWithRoleAsync("asha.mutesi@msp.example", PlatformRoles.Admin);

        (await AuthorizeAsync(TenancyActions.TenantSettingsRead, admin)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, admin)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task A_signed_in_user_may_perform_account_actions()
    {
        var user = await UserWithRoleAsync("ruth.akello@msp.example", platformRole: null);

        (await AuthorizeAsync(IdentityActions.AccountRead, user)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(IdentityActions.AccountLogout, user)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(IdentityActions.AccountLogin, user)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task An_anonymous_caller_may_perform_only_account_actions_that_allow_one()
    {
        (await AuthorizeAsync(IdentityActions.AccountLogin, AnonymousActor.Instance)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(IdentityActions.AccountRead, AnonymousActor.Instance)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    private async Task<ICurrentActor> UserWithRoleAsync(string email, string? platformRole) =>
        new UserActor(await AccountTestData.CreateUserAsync(App.Services, email, TestPasswords.New(), Ct, platformRole: platformRole));

    /// <summary>The codes of <paramref name="actions"/> the evaluator refuses <paramref name="actor"/>.</summary>
    private async Task<List<string>> RefusedAsync(ICurrentActor actor, IEnumerable<ActionDefinition> actions)
    {
        var refused = new List<string>();
        foreach (var action in actions)
        {
            if ((await AuthorizeAsync(action, actor)).IsFailure)
            {
                refused.Add(action.Code.Value);
            }
        }

        return refused;
    }

    private async Task<Result> AuthorizeAsync(ActionDefinition action, ICurrentActor actor)
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityAuthorizationEvaluator>()
            .AuthorizeAsync(new AuthorizationRequest(action, actor), Ct);
    }
}
