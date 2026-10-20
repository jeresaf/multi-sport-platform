using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.Roles;
using PlatformRoleCodes = SportsPlatform.Modules.Identity.Domain.PlatformRoles.PlatformRoles;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>The roles defined in code are checked against the action catalogue at startup (S03 D9, T3 acceptance).</summary>
public sealed class RoleRulesTests
{
    private static readonly IReadOnlyList<ActionCatalogueEntry> _catalogue = ActionCatalogue.Build(ModuleRegistry.All);

    [Fact]
    public void The_system_roles_are_valid()
    {
        RoleRules.Validate(IdentityRoles.All, _catalogue).ShouldBeEmpty();
    }

    [Fact]
    public void A_role_naming_an_unknown_action_is_refused()
    {
        var violations = RoleRules.Validate(With(Tenant("VIEWER", ["tenant.settings.read", "nope.read"])), _catalogue);

        violations.ShouldHaveSingleItem().ShouldBe("Role VIEWER grants 'nope.read', which is not declared in a module's *Actions class.");
    }

    [Fact]
    public void A_role_granting_an_action_of_another_scope_is_refused()
    {
        var violations = RoleRules.Validate(
            With(Tenant("VIEWER", ["tenant.read"]), new RoleDefinition(PlatformRoleCodes.Support, RoleScope.Platform, "Support.", ["tenant.settings.read"])),
            _catalogue);

        violations.ShouldBe(
        [
            "Platform role PLATFORM_SUPPORT grants 'tenant.settings.read', a Tenant action.",
            "Tenant role VIEWER grants 'tenant.read', a Platform action.",
        ], ignoreOrder: true);
    }

    [Fact]
    public void A_tenant_role_granting_an_action_its_assignment_scopes_cannot_grant_is_refused()
    {
        var violations = RoleRules.Validate(With(new RoleDefinition(
            IdentityRoles.TeamManager, RoleScope.Tenant, "Team manager.", ["tenant.settings.read"], [AssignmentScope.Team])), _catalogue);

        violations.ShouldHaveSingleItem().ShouldBe("Role TEAM_MANAGER grants 'tenant.settings.read', which none of its assignment scopes can grant.");
    }

    [Fact]
    public void Role_codes_scopes_and_platform_roles_are_checked()
    {
        var roles = IdentityRoles.All
            .Where(role => role.Code != PlatformRoleCodes.Support)
            .Append(Tenant(IdentityRoles.Viewer, []))
            .Append(new RoleDefinition("PLATFORM_AUDITOR", RoleScope.Platform, "Auditor.", [], [AssignmentScope.Tenant]))
            .Append(new RoleDefinition("STEWARD", RoleScope.Tenant, "Steward.", []))
            .ToList();

        RoleRules.Validate(roles, _catalogue).ShouldBe(
        [
            "Role VIEWER is defined more than once.",
            "Platform role PLATFORM_AUDITOR is not in PlatformRoles, so it can't be assigned.",
            "Platform role PLATFORM_SUPPORT can be assigned but has no definition.",
            "Platform role PLATFORM_AUDITOR can't be assigned at tenant scopes.",
            "Tenant role STEWARD names no scope it can be assigned at.",
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData("nope.read", "not declared")]
    [InlineData("tenant.create", "a Platform action")]
    public void A_bad_role_stops_the_Host_at_startup(string action, string expected)
    {
        using var app = new HostOnlyApp().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton(new RoleCatalogue(With(Tenant(IdentityRoles.Viewer, [action]))))));

        var exception = Should.Throw<InvalidOperationException>(() => app.Services);

        exception.Message.ShouldStartWith("Every role must grant declared actions of its own scope:");
        exception.Message.ShouldContain(expected);
    }

    private static RoleDefinition Tenant(string code, IReadOnlyList<string> actions) =>
        new(code, RoleScope.Tenant, "A test role.", actions, [AssignmentScope.Tenant]);

    /// <summary>The system roles with <paramref name="replacements"/> in place of those with the same codes.</summary>
    private static List<RoleDefinition> With(params RoleDefinition[] replacements) =>
    [
        .. IdentityRoles.All.Where(role => replacements.All(replacement => replacement.Code != role.Code)),
        .. replacements,
    ];
}
