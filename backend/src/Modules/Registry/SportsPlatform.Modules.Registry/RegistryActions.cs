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

    /// <summary>
    /// Needs the duplicate check's token, so it is only granted with the check (pre-review of S06-T2b); answers the
    /// person as <c>person.read</c> does, so it is only granted with that read too (review #98 R1).
    /// </summary>
    public static readonly ActionDefinition PersonCreate = new(
        "person.create",
        "Add a person to the registry after a duplicate check; proceeding past possible duplicates needs a reason, and the pairs are remembered as not the same person.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["person.duplicate_check", "person.read"]);

    /// <summary>
    /// A change to what the duplicate check compares needs its token, so it is only granted with the check; it answers
    /// the person as <c>person.read</c> does, so it is only granted with that read too (as <c>person.create</c>).
    /// </summary>
    public static readonly ActionDefinition PersonUpdate = new(
        "person.update",
        "Change a person's biodata; a change to the names, date of birth or nationalities needs a duplicate check, and proceeding past possible duplicates needs a reason.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["person.duplicate_check", "person.read"]);

    public static readonly ActionDefinition PersonDeactivate = Lifecycle(
        "person.deactivate", "Mark an active person inactive, with an optional reason.", AuditRequirement.Required);

    public static readonly ActionDefinition PersonReactivate = Lifecycle(
        "person.reactivate", "Make an inactive person active again, with an optional reason.", AuditRequirement.Required);

    public static readonly ActionDefinition PersonArchive = Lifecycle(
        "person.archive",
        "Archive an active or inactive person, with a reason: the record and its history stay, and it takes no new profile, alias, identifier, consent or photo.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition PersonUnarchive = Lifecycle(
        "person.unarchive", "Make an archived person active again, with a reason.", AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition PersonMarkDeceased = Lifecycle(
        "person.mark_deceased",
        "Record that an active or inactive person has died, with a reason; final: the record and its history stay.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition PersonAliasAdd = Lifecycle(
        "person.alias.add",
        "Add another name a person is known by (former name, nickname, spelling), so search and the duplicate check find them under it too; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition PersonAliasRemove = Lifecycle(
        "person.alias.remove",
        "Remove a person's alias, with a reason; the alias is kept with who removed it, when and why.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition PersonExternalIdAdd = Lifecycle(
        "person.external_id.add",
        "Add an identifier another authority gave a person (e.g. a registration number); a value any person has or had is refused; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition PersonExternalIdEnd = Lifecycle(
        "person.external_id.end",
        "End a person's external identifier; it is kept, and its value is never given to anyone else.",
        AuditRequirement.Required);

    public static readonly ActionDefinition AthleteProfileCreate = Lifecycle(
        "athlete_profile.create",
        "Record that a person plays a sport variant the tenant has enabled, with positions from its definition; one profile per variant that isn't archived; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition AthleteProfileUpdate = Lifecycle(
        "athlete_profile.update",
        "Change an athlete profile's positions, dominant side and public name.",
        AuditRequirement.Required);

    public static readonly ActionDefinition AthleteProfileStatusChange = Lifecycle(
        "athlete_profile.status.change",
        "Make an athlete profile inactive, retired, active again or archived (final), with a reason; every change is kept.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition StaffProfileCreate = Lifecycle(
        "staff_profile.create",
        "Record that a person works as staff (coach, physio…), with roles from the definitions of sport variants the tenant has enabled; one staff profile that isn't archived; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition StaffProfileUpdate = Lifecycle(
        "staff_profile.update",
        "Change a staff profile's roles and qualifications.",
        AuditRequirement.Required);

    public static readonly ActionDefinition StaffProfileStatusChange = Lifecycle(
        "staff_profile.status.change",
        "Make a staff profile inactive, retired, active again or archived (final), with a reason; every change is kept.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition OfficialProfileCreate = Lifecycle(
        "official_profile.create",
        "Record that a person works as an official (referee, judge…), with roles from the definitions of sport variants the tenant has enabled, grades and licences; one official profile that isn't archived; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition OfficialProfileUpdate = Lifecycle(
        "official_profile.update",
        "Change an official profile's roles, grades and licences.",
        AuditRequirement.Required);

    public static readonly ActionDefinition OfficialProfileStatusChange = Lifecycle(
        "official_profile.status.change",
        "Make an official profile inactive, retired, active again or archived (final), with a reason; every change is kept.",
        AuditRequirement.RequiredWithReason);

    // The lifecycle, alias, identifier and profile actions answer the person as person.read does, so they are only granted with
    // that read.
    private static ActionDefinition Lifecycle(string code, string description, AuditRequirement audit) => new(
        code,
        description,
        ActionScope.Tenant,
        audit,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["person.read"]);
}
