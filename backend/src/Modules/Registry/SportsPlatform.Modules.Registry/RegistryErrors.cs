using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry;

/// <summary>The Registry module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class RegistryErrors
{
    public static readonly Error PersonConcurrencyConflict = new(
        "PERSON_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error VenueConcurrencyConflict = new(
        "VENUE_CONCURRENCY_CONFLICT", "The venue was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error OrganisationConcurrencyConflict = new(
        "ORGANISATION_CONCURRENCY_CONFLICT", "The organisation was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error TeamConcurrencyConflict = new(
        "TEAM_CONCURRENCY_CONFLICT", "The team was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error PersonNotFound = new("PERSON_NOT_FOUND", "The person was not found.", ErrorKind.NotFound);

    public static readonly Error StatusInvalid = new(
        "PERSON_STATUS_INVALID", "The person's current status doesn't allow this change.", ErrorKind.Conflict);

    public static readonly Error NameInvalid = new(
        "PERSON_NAME_INVALID", "A first and a last name each need at least one letter or digit.", ErrorKind.Unprocessable);

    public static readonly Error ExternalIdAuthorityReserved = new(
        "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED",
        "National identity documents (NATIONAL_ID, NIN, PASSPORT) are not collected as external identifiers.",
        ErrorKind.Unprocessable);

    public static readonly Error DateOfBirthInFuture = new(
        "PERSON_DATE_OF_BIRTH_IN_FUTURE", "A date of birth can't be after today (the tenant's date).", ErrorKind.Unprocessable);

    public static readonly Error DuplicateCheckRequired = new(
        "PERSON_DUPLICATE_CHECK_REQUIRED",
        "Run the duplicate check for these details first: the check token is missing, expired, or for other details or another user.",
        ErrorKind.Unprocessable);

    public static readonly Error DuplicateCheckStale = new(
        "PERSON_DUPLICATE_CHECK_STALE",
        "The registry changed since the duplicate check: run it again and review the people it finds.",
        ErrorKind.Conflict);

    public static readonly Error DuplicateOverrideRequired = new(
        "PERSON_DUPLICATE_OVERRIDE_REQUIRED",
        "The duplicate check found people who may be the same person: give a reason to proceed.",
        ErrorKind.Unprocessable);

    public static readonly Error ExternalIdExists = new(
        "PERSON_EXTERNAL_ID_EXISTS",
        "A person already has this external identifier (the same authority and value), or had it: a value is never reused.",
        ErrorKind.Conflict);

    public static readonly Error PersonArchived = new(
        "PERSON_ARCHIVED", "The person is archived: they take no new profile, alias, identifier, consent or photo.", ErrorKind.Conflict);

    public static readonly Error PersonDeceased = new(
        "PERSON_DECEASED", "The person is recorded as deceased: they take no new profile, alias, identifier, consent or photo.", ErrorKind.Conflict);

    /// <summary>Removing the photo of a person who has none (owner, S06-T5b plan).</summary>
    public static readonly Error PhotoNotFound = new("PERSON_PHOTO_NOT_FOUND", "The person has no photo.", ErrorKind.NotFound);

    public static readonly Error AliasNotFound = new("PERSON_ALIAS_NOT_FOUND", "The person has no such alias.", ErrorKind.NotFound);

    public static readonly Error AliasRemoved = new("PERSON_ALIAS_REMOVED", "The alias is already removed.", ErrorKind.Conflict);

    public static readonly Error AliasExists = new(
        "PERSON_ALIAS_EXISTS", "The person is already known by this name: their own name or a current alias gives the same name key.",
        ErrorKind.Conflict);

    public static readonly Error ExternalIdNotFound = new(
        "PERSON_EXTERNAL_ID_NOT_FOUND", "The person has no such external identifier.", ErrorKind.NotFound);

    public static readonly Error ExternalIdEnded = new(
        "PERSON_EXTERNAL_ID_ENDED", "The external identifier has already ended.", ErrorKind.Conflict);

    public static readonly Error ExternalIdValidityInvalid = new(
        "PERSON_EXTERNAL_ID_VALIDITY_INVALID",
        "An identifier's end date can't be before its start date or after today (the tenant's date).",
        ErrorKind.Unprocessable);

    public static readonly Error SportNotEnabled = new(
        "PERSON_SPORT_NOT_ENABLED", "The tenant hasn't enabled this sport variant: enable it first.", ErrorKind.Unprocessable);

    public static readonly Error PositionUnknown = new(
        "PERSON_POSITION_UNKNOWN", "The sport variant's definition has no such position.", ErrorKind.Unprocessable);

    public static readonly Error AthleteProfileExists = new(
        "PERSON_ATHLETE_PROFILE_EXISTS",
        "The person already has an athlete profile for this sport variant: change or reactivate it instead.",
        ErrorKind.Conflict);

    public static readonly Error StaffProfileExists = new(
        "PERSON_STAFF_PROFILE_EXISTS",
        "The person already has a staff profile: change or reactivate it instead.",
        ErrorKind.Conflict);

    public static readonly Error OfficialProfileExists = new(
        "PERSON_OFFICIAL_PROFILE_EXISTS",
        "The person already has an official profile: change or reactivate it instead.",
        ErrorKind.Conflict);

    public static readonly Error RoleUnknown = new(
        "PERSON_ROLE_UNKNOWN", "The sport variant's definition has no such role.", ErrorKind.Unprocessable);

    public static readonly Error ProfileNotFound = new("PERSON_PROFILE_NOT_FOUND", "The person has no such profile.", ErrorKind.NotFound);

    public static readonly Error ProfileArchived = new(
        "PERSON_PROFILE_ARCHIVED", "The profile is archived: it takes no more changes.", ErrorKind.Conflict);

    public static readonly Error ProfileStatusInvalid = new(
        "PERSON_PROFILE_STATUS_INVALID", "The profile's current status doesn't allow this change.", ErrorKind.Conflict);

    public static readonly Error ConsentNotFound = new("PERSON_CONSENT_NOT_FOUND", "The person has no such consent.", ErrorKind.NotFound);

    public static readonly Error ConsentWithdrawn = new(
        "PERSON_CONSENT_WITHDRAWN", "The consent is withdrawn: it takes no more changes.", ErrorKind.Conflict);

    /// <summary>Turning on the public profile of a person who isn't an adult without <c>PUBLIC_PROFILE</c> consent (S06 D15).</summary>
    public static readonly Error ConsentRequired = new(
        "PERSON_CONSENT_REQUIRED", "The person isn't known to be an adult: a guardian's consent with the public profile scope is needed first.", ErrorKind.Unprocessable);

    /// <summary>Recording consent for an adult on the tenant's date (owner, S06-T6a plan).</summary>
    public static readonly Error ConsentNotNeeded = new(
        "PERSON_CONSENT_NOT_NEEDED", "The person is an adult: a guardian's consent is recorded only for minors and people of unknown age.", ErrorKind.Unprocessable);

    public static readonly Error VenueNotFound = new("VENUE_NOT_FOUND", "The venue was not found.", ErrorKind.NotFound);

    public static readonly Error VenueStatusInvalid = new(
        "VENUE_STATUS_INVALID", "The venue's current status doesn't allow this change.", ErrorKind.Conflict);

    public static readonly Error VenueSportUnknown = new(
        "VENUE_SPORT_UNKNOWN", "No published sport definition has this variant.", ErrorKind.Unprocessable);

    public static readonly Error VenueNameInvalid = new(
        "VENUE_NAME_INVALID", "A venue's name needs at least one letter or digit.", ErrorKind.Unprocessable);

    public static readonly Error VenueDuplicateReviewRequired = new(
        "VENUE_DUPLICATE_REVIEW_REQUIRED",
        "Venues already in the registry look like this one: open one of them, or send duplicatesReviewed to add it anyway.",
        ErrorKind.Unprocessable);

    public static readonly Error VenueSlugTaken = new(
        "VENUE_SLUG_TAKEN", "Another venue of the tenant has this slug.", ErrorKind.Unprocessable);

    public static readonly Error VenueArchived = new(
        "VENUE_ARCHIVED", "The venue is archived: it takes no new alias or external identifier.", ErrorKind.Conflict);

    public static readonly Error VenueClosed = new(
        "VENUE_CLOSED", "The venue is closed: it takes no new alias or external identifier. Reopen it first.", ErrorKind.Conflict);

    public static readonly Error VenueAliasExists = new(
        "VENUE_ALIAS_EXISTS", "The venue is already known by this name: its own name or a current alias gives the same name key.",
        ErrorKind.Conflict);

    public static readonly Error VenueAliasNotFound = new("VENUE_ALIAS_NOT_FOUND", "The venue has no such alias.", ErrorKind.NotFound);

    public static readonly Error VenueAliasRemoved = new("VENUE_ALIAS_REMOVED", "The alias is already removed.", ErrorKind.Conflict);

    public static readonly Error VenueExternalIdExists = new(
        "VENUE_EXTERNAL_ID_EXISTS",
        "A venue already has this external identifier (the same authority and value), or had it: a value is never reused.",
        ErrorKind.Conflict);

    public static readonly Error VenueExternalIdNotFound = new(
        "VENUE_EXTERNAL_ID_NOT_FOUND", "The venue has no such external identifier.", ErrorKind.NotFound);

    public static readonly Error VenueExternalIdEnded = new(
        "VENUE_EXTERNAL_ID_ENDED", "The external identifier has already ended.", ErrorKind.Conflict);

    public static readonly Error VenueExternalIdValidityInvalid = new(
        "VENUE_EXTERNAL_ID_VALIDITY_INVALID",
        "An identifier's end date can't be before its start date or after today (the tenant's date).",
        ErrorKind.Unprocessable);

    public static readonly Error OrganisationNotFound = new(
        "ORGANISATION_NOT_FOUND", "The organisation was not found.", ErrorKind.NotFound);

    public static readonly Error OrganisationStatusInvalid = new(
        "ORGANISATION_STATUS_INVALID", "The organisation's current status doesn't allow this change.", ErrorKind.Conflict);

    public static readonly Error OrganisationNameInvalid = new(
        "ORGANISATION_NAME_INVALID", "An organisation's display name needs at least one letter or digit.", ErrorKind.Unprocessable);

    public static readonly Error OrganisationDuplicateReviewRequired = new(
        "ORGANISATION_DUPLICATE_REVIEW_REQUIRED",
        "Organisations already in the registry look like this one: open one of them, or send duplicatesReviewed to add it anyway.",
        ErrorKind.Unprocessable);

    public static readonly Error OrganisationSlugTaken = new(
        "ORGANISATION_SLUG_TAKEN", "Another organisation of the tenant has this slug.", ErrorKind.Unprocessable);

    public static readonly Error OrganisationPrimaryVenueUnavailable = new(
        "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE",
        "The primary venue must be a venue of the tenant that isn't closed or archived.",
        ErrorKind.Unprocessable);

    public static readonly Error OrganisationArchived = new(
        "ORGANISATION_ARCHIVED",
        "The organisation is archived: it takes no new parent, alias or external identifier. Unarchive it first.", ErrorKind.Conflict);

    public static readonly Error OrganisationDissolved = new(
        "ORGANISATION_DISSOLVED", "The organisation is dissolved: it takes no new parent, alias or external identifier.", ErrorKind.Conflict);

    public static readonly Error OrganisationParentUnavailable = new(
        "ORGANISATION_PARENT_UNAVAILABLE",
        "The parent must be another organisation of the tenant that isn't dissolved or archived.",
        ErrorKind.Unprocessable);

    public static readonly Error OrganisationParentCycle = new(
        "ORGANISATION_PARENT_CYCLE",
        "The parent can't be the organisation itself or one of the organisations below it: the links would close a cycle.",
        ErrorKind.Unprocessable);

    public static readonly Error OrganisationAliasExists = new(
        "ORGANISATION_ALIAS_EXISTS",
        "The organisation is already known by this name: its display, legal or short name or a current alias gives the same name key.",
        ErrorKind.Conflict);

    public static readonly Error OrganisationAliasNotFound = new(
        "ORGANISATION_ALIAS_NOT_FOUND", "The organisation has no such alias.", ErrorKind.NotFound);

    public static readonly Error OrganisationAliasRemoved = new(
        "ORGANISATION_ALIAS_REMOVED", "The alias is already removed.", ErrorKind.Conflict);

    public static readonly Error OrganisationExternalIdExists = new(
        "ORGANISATION_EXTERNAL_ID_EXISTS",
        "An organisation already has this external identifier (the same authority and value), or had it: a value is never reused.",
        ErrorKind.Conflict);

    public static readonly Error OrganisationExternalIdNotFound = new(
        "ORGANISATION_EXTERNAL_ID_NOT_FOUND", "The organisation has no such external identifier.", ErrorKind.NotFound);

    public static readonly Error OrganisationExternalIdEnded = new(
        "ORGANISATION_EXTERNAL_ID_ENDED", "The external identifier has already ended.", ErrorKind.Conflict);

    public static readonly Error OrganisationExternalIdValidityInvalid = new(
        "ORGANISATION_EXTERNAL_ID_VALIDITY_INVALID",
        "An identifier's end date can't be before its start date or after today (the tenant's date).",
        ErrorKind.Unprocessable);

    public static readonly Error TeamNotFound = new("TEAM_NOT_FOUND", "The team was not found.", ErrorKind.NotFound);

    public static readonly Error TeamNameInvalid = new(
        "TEAM_NAME_INVALID", "A team's name needs at least one letter or digit.", ErrorKind.Unprocessable);

    public static readonly Error TeamSportNotEnabled = new(
        "TEAM_SPORT_NOT_ENABLED", "The sport isn't enabled for your organisation: a team can only be created for an enabled sport.",
        ErrorKind.Unprocessable);

    public static readonly Error TeamOrganisationUnavailable = new(
        "TEAM_ORGANISATION_UNAVAILABLE",
        "The organisation must be an organisation of the tenant that isn't dissolved or archived.",
        ErrorKind.Unprocessable);

    public static readonly Error TeamHomeVenueUnavailable = new(
        "TEAM_HOME_VENUE_UNAVAILABLE", "The home venue must be a venue of the tenant that isn't closed or archived.",
        ErrorKind.Unprocessable);

    public static readonly Error TeamColoursInvalid = new(
        "TEAM_COLOURS_INVALID", "A secondary colour needs a primary colour, and a tertiary colour a secondary one.",
        ErrorKind.Unprocessable);

    public static readonly Error TeamSlugTaken = new(
        "TEAM_SLUG_TAKEN", "Another team of the tenant has this slug.", ErrorKind.Unprocessable);

    public static readonly Error TeamDuplicateReviewRequired = new(
        "TEAM_DUPLICATE_REVIEW_REQUIRED",
        "Teams already in the registry look like this one: open one of them, or send duplicatesReviewed to add it anyway.",
        ErrorKind.Unprocessable);

    public static readonly Error TeamRenameRequired = new(
        "TEAM_RENAME_REQUIRED",
        "An update keeps the team's name: rename the team (POST /teams/{teamId}/rename) so the old name is kept with its dates.",
        ErrorKind.Unprocessable);

    public static readonly Error TeamStatusInvalid = new(
        "TEAM_STATUS_INVALID", "The team's current status doesn't allow this change.", ErrorKind.Conflict);
}
