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
        "View a person's private data: the full date of birth, and the guardians' names, relationships and contacts and the evidence of their consents. Every read is audited.",
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

    /// <summary>
    /// Stores a new file, so it names <c>STORAGE_GB</c>: the handler checks the quota before storing (S06 D20).
    /// </summary>
    public static readonly ActionDefinition PersonPhotoSet = new(
        "person.photo.set",
        "Upload a person's photo (JPEG, PNG or WebP up to 5 MB), replacing the one they had; not for an archived or deceased person.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        entitlement: "STORAGE_GB",
        grantedWith: ["person.read"]);

    public static readonly ActionDefinition PersonPhotoRemove = Lifecycle(
        "person.photo.remove",
        "Remove a person's photo, whatever the person's status.",
        AuditRequirement.Required);

    public static readonly ActionDefinition PersonConsentRecord = Lifecycle(
        "person.consent.record",
        "Record a guardian's consent (data processing, public profile, photo) for a person who is a minor or whose age is unknown; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition PersonConsentWithdraw = Lifecycle(
        "person.consent.withdraw",
        "Withdraw some or all scopes of a guardian's consent, with a reason; the record is kept and the remaining scopes stay in force.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition PersonPublicProfileEnable = Lifecycle(
        "person.public_profile.enable",
        "Turn on a person's public profile; a person who isn't known to be an adult needs a guardian's consent with the public profile scope; not for an archived or deceased person.",
        AuditRequirement.Required);

    public static readonly ActionDefinition PersonPublicProfileDisable = Lifecycle(
        "person.public_profile.disable",
        "Turn off a person's public profile, whatever the person's status.",
        AuditRequirement.Required);

    /// <summary>
    /// Stores a new file, so it names <c>STORAGE_GB</c>: the handler checks the quota before storing (S06 D20).
    /// </summary>
    public static readonly ActionDefinition PersonConsentEvidenceUpload = new(
        "person.consent_evidence.upload",
        "Upload the evidence of a guardian's consent (a PDF, JPEG or PNG up to 10 MB, such as a signed form), replacing the one it had; not for a withdrawn consent or an archived or deceased person.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        entitlement: "STORAGE_GB",
        grantedWith: ["person.read"]);

    public static readonly ActionDefinition VenueRead = new(
        "venue.read",
        "View the venues directory and a venue's record: address, time zone, capacity, surfaces, supported sports and status.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    /// <summary>
    /// Its own unaudited action, as <c>person.duplicate_check</c>: the check saves nothing (S07 D13). It answers other
    /// venues' names and cities, so it is only granted with <c>venue.read</c>.
    /// </summary>
    public static readonly ActionDefinition VenueDuplicateCheck = new(
        "venue.duplicate_check",
        "Check a new or changed venue's name, city and coordinates against the venues already in the registry.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["venue.read"]);

    /// <summary>
    /// Runs the duplicate check itself (no token, S07 D13), and answers the venue as <c>venue.read</c> does, so it is
    /// granted with the check and that read.
    /// </summary>
    public static readonly ActionDefinition VenueCreate = new(
        "venue.create",
        "Add a venue to the registry; possible duplicates are shown first, and creating it anyway is recorded with the venues shown.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["venue.duplicate_check", "venue.read"]);

    /// <summary>
    /// Runs the duplicate check again when the name, city or coordinates change (S07 D11, D13), and answers the venue as
    /// <c>venue.read</c> does, so it is granted with the check and that read.
    /// </summary>
    public static readonly ActionDefinition VenueUpdate = new(
        "venue.update",
        "Change a venue's details; a change to its name, city or coordinates shows possible duplicates first, and saving it anyway is recorded with the venues shown.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["venue.duplicate_check", "venue.read"]);

    /// <summary>One action for every move of the venue lifecycle (S07 D12), with a reason; answers the venue as <c>venue.read</c> does.</summary>
    public static readonly ActionDefinition VenueStatusChange = new(
        "venue.status.change",
        "Mark a venue temporarily unavailable, closed or archived, reopen it or unarchive it, with a reason.",
        ActionScope.Tenant,
        AuditRequirement.RequiredWithReason,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["venue.read"]);

    public static readonly ActionDefinition VenueAliasAdd = VenueChild(
        "venue.alias.add",
        "Add another name a venue is known by (former name, nickname, spelling), so the directory finds it under it too; not for an archived or closed venue.",
        AuditRequirement.Required);

    public static readonly ActionDefinition VenueAliasRemove = VenueChild(
        "venue.alias.remove",
        "Remove a venue's alias, with a reason; the alias is kept with who removed it, when and why.",
        AuditRequirement.RequiredWithReason);

    public static readonly ActionDefinition VenueExternalIdAdd = VenueChild(
        "venue.external_id.add",
        "Add an identifier another authority gave a venue (e.g. a facility number); a value any venue has or had is refused; not for an archived or closed venue.",
        AuditRequirement.Required);

    public static readonly ActionDefinition VenueExternalIdEnd = VenueChild(
        "venue.external_id.end",
        "End a venue's external identifier; it is kept, and its value is never given to another venue.",
        AuditRequirement.Required);

    public static readonly ActionDefinition OrganisationRead = new(
        "organisation.read",
        "View the organisations directory and an organisation's record: type, names, city, contacts, primary venue and status.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        availableWhenReadOnly: true);

    /// <summary>
    /// Its own unaudited action, as <c>venue.duplicate_check</c>: the check saves nothing (S07 D13). It answers other
    /// organisations' names and cities, so it is only granted with <c>organisation.read</c>.
    /// </summary>
    public static readonly ActionDefinition OrganisationDuplicateCheck = new(
        "organisation.duplicate_check",
        "Check a new or changed organisation's names, type and city against the organisations already in the registry.",
        ActionScope.Tenant,
        AuditRequirement.None,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["organisation.read"]);

    /// <summary>
    /// Runs the duplicate check itself (no token, S07 D13), and answers the organisation as <c>organisation.read</c>
    /// does, so it is granted with the check and that read.
    /// </summary>
    public static readonly ActionDefinition OrganisationCreate = new(
        "organisation.create",
        "Add an organisation (a club, school, federation…) to the registry; possible duplicates are shown first, and creating it anyway is recorded with the organisations shown.",
        ActionScope.Tenant,
        AuditRequirement.Required,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["organisation.duplicate_check", "organisation.read"]);

    // The lifecycle, alias, identifier, profile, photo, consent and public-profile actions answer the person as person.read does, so they are only
    // granted with that read.
    private static ActionDefinition Lifecycle(string code, string description, AuditRequirement audit) => new(
        code,
        description,
        ActionScope.Tenant,
        audit,
        since: "S06",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["person.read"]);

    // A venue's alias and identifier actions (S07 D10) answer the venue as venue.read does, so they are only granted with
    // that read.
    private static ActionDefinition VenueChild(string code, string description, AuditRequirement audit) => new(
        code,
        description,
        ActionScope.Tenant,
        audit,
        since: "S07",
        scopableBy: [AssignmentScope.Tenant],
        grantedWith: ["venue.read"]);
}
