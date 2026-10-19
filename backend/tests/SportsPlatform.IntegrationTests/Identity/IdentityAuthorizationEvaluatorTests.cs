using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// The evaluator's account step (S03 D10): a signed-in user may work on their own account; an anonymous caller only
/// where the action allows one. Platform and tenant actions are refused until T3 and T4 (fail closed).
/// </summary>
public sealed class IdentityAuthorizationEvaluatorTests
{
    private static readonly IdentityAuthorizationEvaluator _evaluator = new();

    private static readonly ICurrentActor _user = new UserActor(new Guid("0192a1b2-0000-7000-8000-00000000d001"));

    [Fact]
    public async Task A_signed_in_user_may_perform_account_actions()
    {
        (await AuthorizeAsync(IdentityActions.AccountRead, _user)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(IdentityActions.AccountLogout, _user)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(IdentityActions.AccountLogin, _user)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task An_anonymous_caller_may_perform_only_account_actions_that_allow_one()
    {
        (await AuthorizeAsync(IdentityActions.AccountLogin, AnonymousActor.Instance)).IsSuccess.ShouldBeTrue();
        (await AuthorizeAsync(IdentityActions.AccountRead, AnonymousActor.Instance)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    [Fact]
    public async Task Platform_and_tenant_actions_are_refused_until_roles_exist()
    {
        (await AuthorizeAsync(TenancyActions.TenantCreate, _user)).Error.ShouldBe(AuthorizationErrors.Forbidden);
        (await AuthorizeAsync(AuthorizationTestActions.FixtureRead, _user)).Error.ShouldBe(AuthorizationErrors.Forbidden);
    }

    private static async Task<Result> AuthorizeAsync(ActionDefinition action, ICurrentActor actor) =>
        await _evaluator.AuthorizeAsync(new AuthorizationRequest(action, actor), TestContext.Current.CancellationToken);
}
