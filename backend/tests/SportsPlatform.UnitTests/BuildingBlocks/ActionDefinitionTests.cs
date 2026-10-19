using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class ActionDefinitionTests
{
    [Fact]
    public void Tenant_action_keeps_its_fields_and_orders_scopes()
    {
        var action = new ActionDefinition(
            "fixture.reschedule",
            "Move a fixture to another date or venue.",
            ActionScope.Tenant,
            AuditRequirement.RequiredWithReason,
            since: "S10",
            scopableBy: [AssignmentScope.Competition, AssignmentScope.Tenant, AssignmentScope.Competition],
            entitlement: "fixtures.scheduling",
            idempotent: true);

        action.Code.ShouldBe(new ActionCode("fixture.reschedule"));
        action.Scope.ShouldBe(ActionScope.Tenant);
        action.ScopableBy.ShouldBe([AssignmentScope.Tenant, AssignmentScope.Competition]);
        action.Audit.ShouldBe(AuditRequirement.RequiredWithReason);
        action.Entitlement.ShouldBe("fixtures.scheduling");
        action.Idempotent.ShouldBeTrue();
        action.Since.ShouldBe("S10");
    }

    [Fact]
    public void Platform_action_has_no_assignment_scopes()
    {
        var action = new ActionDefinition("platform.outbox.read", "View the outbox.", ActionScope.Platform, AuditRequirement.Required, "S01");

        action.ScopableBy.ShouldBeEmpty();
        action.Entitlement.ShouldBeNull();
        action.Idempotent.ShouldBeFalse();
    }

    [Fact]
    public void Platform_action_scopable_by_a_tenant_scope_is_rejected()
    {
        Should.Throw<ArgumentException>(() => new ActionDefinition(
            "platform.outbox.read", "View the outbox.", ActionScope.Platform, AuditRequirement.Required, "S01", [AssignmentScope.Tenant]));
    }

    [Theory]
    [InlineData(new AssignmentScope[0])]
    [InlineData(new[] { AssignmentScope.Contest })]
    public void Tenant_action_not_scopable_by_tenant_is_rejected(AssignmentScope[] scopes)
    {
        Should.Throw<ArgumentException>(() => new ActionDefinition(
            "contest.start", "Start a contest.", ActionScope.Tenant, AuditRequirement.Required, "S11", scopes));
    }

    [Theory]
    [InlineData("Contest.Start", "Start a contest.", "S11", null)]
    [InlineData("contest.start", " ", "S11", null)]
    [InlineData("contest.start", "Start a contest.", "", null)]
    [InlineData("contest.start", "Start a contest.", "S11", " ")]
    public void Malformed_fields_are_rejected(string code, string description, string since, string? entitlement)
    {
        Should.Throw<ArgumentException>(() => new ActionDefinition(
            code, description, ActionScope.Tenant, AuditRequirement.None, since, [AssignmentScope.Tenant], entitlement));
    }

    [Fact]
    public void Account_action_may_allow_anonymous_callers()
    {
        var action = new ActionDefinition(
            "account.login", "Sign in.", ActionScope.Account, AuditRequirement.Required, "S03", allowsAnonymous: true);

        action.Scope.ShouldBe(ActionScope.Account);
        action.AllowsAnonymous.ShouldBeTrue();
        action.ScopableBy.ShouldBeEmpty();
    }

    [Fact]
    public void Actions_need_a_user_unless_they_allow_anonymous_callers()
    {
        new ActionDefinition("account.read", "Read the account.", ActionScope.Account, AuditRequirement.None, "S03")
            .AllowsAnonymous.ShouldBeFalse();
    }

    // S03 D2: only the caller's own account can be worked on before signing in.
    [Theory]
    [InlineData(ActionScope.Platform)]
    [InlineData(ActionScope.Tenant)]
    public void Only_account_actions_may_allow_anonymous_callers(ActionScope scope)
    {
        AssignmentScope[] scopes = scope == ActionScope.Tenant ? [AssignmentScope.Tenant] : [];

        Should.Throw<ArgumentException>(() => new ActionDefinition(
            "tenant.read", "Read tenants.", scope, AuditRequirement.None, "S03", scopes, allowsAnonymous: true));
    }

    [Fact]
    public void Account_action_scopable_by_a_tenant_scope_is_rejected()
    {
        Should.Throw<ArgumentException>(() => new ActionDefinition(
            "account.read", "Read the account.", ActionScope.Account, AuditRequirement.None, "S03", [AssignmentScope.Tenant]));
    }
}
