using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Registry;

/// <summary>The Registry module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class RegistryActions
{
    public static readonly ActionDefinition PersonRead = new(
        "person.read",
        "View the people directory and a person's record: names, aliases, external identifiers, status and its history, birth year and whether the person is a minor.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    public static readonly ActionDefinition PersonReadPrivate = new(
        "person.read_private",
        "View a person's private data: the full date of birth. Every read is audited.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);
}
