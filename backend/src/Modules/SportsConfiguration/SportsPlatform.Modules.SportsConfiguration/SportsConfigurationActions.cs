using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.SportsConfiguration;

/// <summary>The Sports Configuration module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class SportsConfigurationActions
{
    // Global configuration, no tenant data: no read is audited (S05 D14).
    public static readonly ActionDefinition SportRead = new(
        "sport.read",
        "View the published sport definitions: periods, events, positions and roles, statistics, scorer pad.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S05",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    public static readonly ActionDefinition FormatRead = new(
        "format.read",
        "View the published competition formats: stages, generators and their parameters.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S05",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    public static readonly ActionDefinition RulesetRead = new(
        "ruleset.read",
        "View the published ruleset templates: their sport, formats, rules and the parameters a competition may change.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S05",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    // Platform staff read every kind of global configuration with one action (D14): definitions, formats, templates.
    public static readonly ActionDefinition PlatformSportRead = new(
        "platform.sport.read",
        "View the published sport definitions, formats and ruleset templates.",
        ActionScope.Platform,
        AuditRequirement.None,
        since: "S05");
}
