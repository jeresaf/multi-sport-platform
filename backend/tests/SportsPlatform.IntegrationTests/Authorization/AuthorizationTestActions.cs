using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.IntegrationTests.Authorization;

/// <summary>Actions for test endpoints; never part of a module's catalogue.</summary>
public static class AuthorizationTestActions
{
    public static readonly ActionDefinition FixtureRead = new(
        "fixture.read", "View fixtures.", ActionScope.Tenant, AuditRequirement.None, "S10", [AssignmentScope.Tenant, AssignmentScope.Competition]);

    public static readonly ActionDefinition FixtureCreate = new(
        "fixture.create", "Create a fixture.", ActionScope.Tenant, AuditRequirement.Required, "S10", [AssignmentScope.Tenant, AssignmentScope.Competition]);
}
