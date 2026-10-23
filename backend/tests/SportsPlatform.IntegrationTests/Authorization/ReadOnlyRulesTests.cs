using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Host.Catalogues;
using SportsPlatform.Host.Modules;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>Every tenant read stays available while the tenant is read-only, checked at startup (S04 D8).</summary>
public sealed class ReadOnlyRulesTests
{
    [Fact]
    public void The_declared_actions_are_valid()
    {
        ReadOnlyRules.Validate(ActionCatalogue.Build(ModuleRegistry.All)).ShouldBeEmpty();
    }

    [Fact]
    public void A_tenant_read_not_available_when_read_only_is_refused()
    {
        ActionCatalogueEntry[] actions =
        [
            new(Tenant("fixture.read", availableWhenReadOnly: false), "Competitions"),
            new(Tenant("fixture.schedule", availableWhenReadOnly: false), "Competitions"),
            new(Tenant("result.submit", availableWhenReadOnly: true), "Contests"),
            new(new ActionDefinition("tenant.read", "Read tenants.", ActionScope.Platform, AuditRequirement.Required, "S02"), "Tenancy"),
        ];

        ReadOnlyRules.Validate(actions).ShouldHaveSingleItem()
            .ShouldBe("Tenant read 'fixture.read' must be availableWhenReadOnly: a read-only tenant keeps reading.");
    }

    private static ActionDefinition Tenant(string code, bool availableWhenReadOnly) => new(
        code, "A test action.", ActionScope.Tenant, AuditRequirement.None, "S04", [AssignmentScope.Tenant],
        availableWhenReadOnly: availableWhenReadOnly);
}
