using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Registry;

/// <summary>The Registry module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class RegistryErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "PERSON_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload and try again.", ErrorKind.Conflict);

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
}
