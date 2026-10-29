using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Features.Invitations;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>Actions and plans are checked against the entitlement catalogue at startup (S04 D1, T1 acceptance).</summary>
public sealed class EntitlementRulesTests
{
    private static readonly IReadOnlyList<ActionCatalogueEntry> _actions = ActionCatalogue.Build(ModuleRegistry.All);
    private static readonly DeclaredEntitlements _entitlements = new(ModuleRegistry.All);

    [Fact]
    public void The_declared_actions_and_plans_are_valid()
    {
        EntitlementRules.Validate(_actions, _entitlements, TenancyPlans.All).ShouldBeEmpty();
    }

    [Fact]
    public void An_action_naming_an_unknown_entitlement_is_refused()
    {
        var violations = EntitlementRules.Validate([Tenant("fixture.reschedule", "FIXTURE_SCHEDULING")], _entitlements, TenancyPlans.All);

        violations.ShouldHaveSingleItem()
            .ShouldBe("Action 'fixture.reschedule' names entitlement FIXTURE_SCHEDULING, which is not declared in a module's *Entitlements class.");
    }

    [Fact]
    public void A_platform_or_account_action_naming_an_entitlement_is_refused()
    {
        ActionCatalogueEntry[] actions =
        [
            new(new ActionDefinition("plan.review", "Review plans.", ActionScope.Platform, AuditRequirement.None, "S04", entitlement: "API_ACCESS"), "Tenancy"),
            new(new ActionDefinition("account.key.create", "Create a key.", ActionScope.Account, AuditRequirement.None, "S04", entitlement: "API_ACCESS"), "Identity"),
        ];

        EntitlementRules.Validate(actions, _entitlements, TenancyPlans.All).ShouldBe(
        [
            "Platform action 'plan.review' names entitlement API_ACCESS; only tenant actions can.",
            "Account action 'account.key.create' names entitlement API_ACCESS; only tenant actions can.",
        ]);
    }

    [Fact]
    public void A_plan_must_value_every_declared_entitlement_with_its_type_and_nothing_else()
    {
        var values = TenancyPlans.Standard.Values
            .Where(entry => entry.Key != "MAX_TEAMS")
            .ToDictionary(StringComparer.Ordinal);
        values["REPORTS"] = EntitlementValue.Level("FULL");
        values["LIVE_SCORING"] = EntitlementValue.Quota(1);
        values["FIXTURE_SCHEDULING"] = EntitlementValue.Flag(true);
        var plan = new PlanDefinition("STANDARD", "Standard", 1, values);

        EntitlementRules.Validate([], _entitlements, [plan]).ShouldBe(
        [
            "Plan STANDARD gives entitlement LIVE_SCORING the value 1, which doesn't fit its type or levels.",
            "Plan STANDARD gives entitlement MAX_TEAMS no value.",
            "Plan STANDARD gives entitlement REPORTS the value FULL, which doesn't fit its type or levels.",
            "Plan STANDARD values FIXTURE_SCHEDULING, which is not a declared entitlement.",
        ], ignoreOrder: true);
    }

    [Fact]
    public void Plan_codes_and_ranks_are_unique()
    {
        var copy = new PlanDefinition("PRO", "Pro again", 1, TenancyPlans.Pro.Values);

        EntitlementRules.Validate([], _entitlements, [.. TenancyPlans.All, copy]).ShouldBe(
        [
            "Plan PRO is defined more than once.",
            "Plans STANDARD, PRO share rank 1.",
        ], ignoreOrder: true);
    }

    // T1 acceptance: membership.invite names MAX_STAFF_USERS; without that entitlement declared, the Host won't build.
    [Fact]
    public void An_action_naming_an_undeclared_entitlement_stops_the_Host_at_startup()
    {
        var withoutStaffUsers = _entitlements.All.Where(entitlement => entitlement != TenancyEntitlements.MaxStaffUsers);
        using var app = new HostOnlyApp().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEntitlementCatalogue>(new DeclaredEntitlements(withoutStaffUsers))));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldStartWith("Actions, plans and usage counters must name declared entitlements:");
        exception.Message.ShouldContain("Action 'membership.invite' names entitlement MAX_STAFF_USERS");
    }

    // #63 R1: the rule reads the plans service, so a test can declare other plans (S04 D20).
    [Fact]
    public void The_Host_checks_the_plans_of_the_plan_catalogue_at_startup()
    {
        var withoutTeams = TenancyPlans.Pro.Values.Where(entry => entry.Key != "MAX_TEAMS").ToDictionary(StringComparer.Ordinal);
        using var app = new HostOnlyApp().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPlanCatalogue>(new PlanCatalogue([TenancyPlans.Standard, new PlanDefinition("PRO", "Pro", 2, withoutTeams)]))));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldStartWith("Actions, plans and usage counters must name declared entitlements:");
        exception.Message.ShouldContain("Plan PRO gives entitlement MAX_TEAMS no value.");
    }

    // S04 D15: each quota has at most one usage counter, and only a declared quota is counted.
    [Fact]
    public void Usage_counters_count_declared_quotas_once_each()
    {
        EntitlementRules.ValidateQuotaUsages([StaffUsers.Code], _entitlements).ShouldBeEmpty();

        EntitlementRules.ValidateQuotaUsages(["MAX_STAFF_USERS", "MAX_STAFF_USERS", "CUSTOM_DOMAIN", "MAX_REFEREES"], _entitlements).ShouldBe(
        [
            "A usage counter counts CUSTOM_DOMAIN, a Boolean entitlement; only quotas are counted.",
            "A usage counter counts MAX_REFEREES, which is not a declared entitlement.",
            "MAX_STAFF_USERS has more than one usage counter.",
        ]);
    }

    [Fact]
    public void A_second_usage_counter_for_a_quota_stops_the_Host_at_startup()
    {
        using var app = new HostOnlyApp().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddQuotaUsage<SecondStaffUserCounter>(StaffUsers.Code)));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldStartWith("Actions, plans and usage counters must name declared entitlements:");
        exception.Message.ShouldContain("MAX_STAFF_USERS has more than one usage counter.");
    }

    private sealed class SecondStaffUserCounter : IQuotaUsage
    {
        public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private static ActionCatalogueEntry Tenant(string code, string entitlement) =>
        new(new ActionDefinition(code, "A test action.", ActionScope.Tenant, AuditRequirement.None, "S04", [AssignmentScope.Tenant], entitlement), "Test");
}
