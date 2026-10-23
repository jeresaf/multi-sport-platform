using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.IntegrationTests.Catalogues;

/// <summary><c>docs/generated/roles.md</c> and <c>contracts/roles.json</c> (S03 D9): who can do what.</summary>
public sealed class RoleCatalogueTests
{
    private static readonly RoleCatalogue _roles = new(IdentityRoles.All);

    [Fact]
    public void The_system_roles_are_the_platform_roles_then_the_tenant_roles_of_authorization_md()
    {
        _roles.Roles.Select(role => role.Code).ShouldBe(
        [
            "PLATFORM_ADMIN", "PLATFORM_SUPPORT",
            "TENANT_OWNER", "TENANT_ADMIN", "REGISTRAR", "COMPETITION_MANAGER", "RESULTS_OFFICER", "SCORER", "TEAM_MANAGER",
            "DATA_ENTRY", "VIEWER",
        ]);
    }

    // Owner (S03-T3): admin holds every platform action, support exactly the reads; a new platform action fails here.
    [Fact]
    public void Platform_admin_holds_every_platform_action_and_support_the_reads()
    {
        var platformActions = ActionCatalogue.Build(ModuleRegistry.All)
            .Where(entry => entry.Action.Scope == ActionScope.Platform)
            .Select(entry => entry.Action.Code.Value)
            .ToList();

        _roles.Find("PLATFORM_ADMIN").ShouldNotBeNull().Actions.ShouldBe(platformActions, ignoreOrder: true);
        _roles.Find("PLATFORM_SUPPORT").ShouldNotBeNull().Actions
            .ShouldBe(platformActions.Where(code => code.EndsWith(".read", StringComparison.Ordinal)), ignoreOrder: true);
    }

    // Owner (S03-T3): every role assigned tenant-wide reads the settings; owners and admins change them.
    [Fact]
    public void Tenant_roles_assigned_tenant_wide_read_the_settings_and_only_owners_and_admins_change_them()
    {
        var tenantRoles = _roles.Roles.Where(role => role.Scope == RoleScope.Tenant).ToList();

        tenantRoles.Where(role => role.Actions.Contains("tenant.settings.read")).Select(role => role.Code)
            .ShouldBe(tenantRoles.Where(role => role.AssignableAt.Contains(AssignmentScope.Tenant)).Select(role => role.Code));
        tenantRoles.Where(role => role.Actions.Contains("tenant.settings.update")).Select(role => role.Code)
            .ShouldBe([IdentityRoles.TenantOwner, IdentityRoles.TenantAdmin]);
        _roles.Find(IdentityRoles.TeamManager).ShouldNotBeNull().Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Every_role_assigned_tenant_wide_and_every_platform_role_reads_its_capabilities()
    {
        var tenantRoles = _roles.Roles.Where(role => role.Scope == RoleScope.Tenant).ToList();

        tenantRoles.Where(role => role.Actions.Contains("tenant.capabilities.read")).Select(role => role.Code)
            .ShouldBe(tenantRoles.Where(role => role.AssignableAt.Contains(AssignmentScope.Tenant)).Select(role => role.Code));
        _roles.Roles.Where(role => role.Actions.Contains("platform.capabilities.read")).Select(role => role.Code)
            .ShouldBe(_roles.Roles.Where(role => role.Scope == RoleScope.Platform).Select(role => role.Code));
    }

    // S03 D11: what stays open on a suspended or archived tenant is the owner's alone.
    [Fact]
    public void Only_the_tenant_owner_holds_the_actions_available_when_blocked()
    {
        var blockedActions = ActionCatalogue.Build(ModuleRegistry.All)
            .Where(entry => entry.Action.AvailableWhenBlocked)
            .Select(entry => entry.Action.Code.Value)
            .ToList();

        blockedActions.ShouldBe(["tenant.access.read"]);
        _roles.Roles.Where(role => role.Actions.Intersect(blockedActions).Any()).Select(role => role.Code)
            .ShouldBe([IdentityRoles.TenantOwner]);
    }

    [Fact]
    public void The_setup_roles_are_the_tenant_owner_and_admin_assigned_tenant_wide()
    {
        IdentityRoles.SetupRoles.ShouldBe([IdentityRoles.TenantOwner, IdentityRoles.TenantAdmin]);
        IdentityRoles.SetupRoles.ShouldAllBe(code => _roles.Find(code)!.AssignableAt.Contains(AssignmentScope.Tenant));
    }

    [Fact]
    public void Markdown_and_JSON_carry_every_field()
    {
        var markdown = RoleCatalogueExport.ToMarkdown(_roles);
        var json = RoleCatalogueExport.ToJson(_roles);

        markdown.ShouldContain("**Actions** is what a role grants today; **Description** is the role's purpose");
        markdown.ShouldContain("| `VIEWER` | TENANT | TENANT | `membership.read`, `tenant.capabilities.read`, `tenant.settings.read` | Read-only admin. |\n");
        markdown.ShouldContain("| `SCORER` | TENANT | TENANT, COMPETITION, CONTEST | `tenant.capabilities.read`, `tenant.settings.read` |");
        markdown.ShouldContain("| `TEAM_MANAGER` | TENANT | TEAM | — |");
        markdown.ShouldContain("| `PLATFORM_SUPPORT` | PLATFORM | — | `plan.read`, `platform.audit.read`, `platform.capabilities.read`, `platform.outbox.read`, `subscription.read`, `tenant.read` |");
        markdown.ShouldNotContain("\r");
        json.ShouldNotContain("\r");
        json.ShouldEndWith("}\n");
        using var document = JsonDocument.Parse(json);
        var roles = document.RootElement.GetProperty("roles").EnumerateArray().ToList();
        roles.Select(role => role.GetProperty("code").GetString()).ShouldBe(_roles.Roles.Select(role => role.Code));
        var owner = roles.Single(role => role.GetProperty("code").GetString() == "TENANT_OWNER");
        owner.GetProperty("scope").GetString().ShouldBe("TENANT");
        owner.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
        owner.GetProperty("assignableAt").EnumerateArray().Select(scope => scope.GetString()).ShouldBe(["TENANT"]);
        owner.GetProperty("actions").EnumerateArray().Select(action => action.GetString()).ShouldBe(
        [
            "invitation.revoke", "membership.invite", "membership.read", "membership.remove", "membership.restore",
            "membership.suspend", "role.assign", "role.revoke", "tenant.access.read", "tenant.capabilities.read", "tenant.settings.read",
            "tenant.settings.update",
        ]);
    }
}
