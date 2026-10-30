using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.SportsConfiguration;

/// <summary>The Sports Configuration module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class SportsConfigurationActions
{
    // Global configuration, no tenant data: neither read is audited (S05 D14).
    public static readonly ActionDefinition SportRead = new(
        "sport.read",
        "View the published sport definitions: periods, events, positions and roles, statistics, scorer pad.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S05",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    public static readonly ActionDefinition PlatformSportRead = new(
        "platform.sport.read",
        "View the published sport definitions.",
        ActionScope.Platform,
        AuditRequirement.None,
        since: "S05");
}
