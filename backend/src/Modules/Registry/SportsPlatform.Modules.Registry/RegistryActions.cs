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

    /// <summary>
    /// Its own action, not <c>person.create</c>: the check saves nothing and is not audited, and the pipeline audits
    /// every request of an audited action (owner, S06-T2a plan). Its evidence can confirm a date of birth the caller
    /// guessed, so it is only granted with <c>person.read_private</c> (owner, Q42).
    /// </summary>
    public static readonly ActionDefinition PersonDuplicateCheck = new(
        "person.duplicate_check",
        "Check a new or changed person's names, date of birth, nationality and external identifiers against the people already in the registry.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["person.read_private"]);

    public static readonly ActionDefinition PersonCreate = new(
        "person.create",
        "Add a person to the registry after a duplicate check; proceeding past possible duplicates needs a reason, and the pairs are remembered as not the same person.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant]);
}
