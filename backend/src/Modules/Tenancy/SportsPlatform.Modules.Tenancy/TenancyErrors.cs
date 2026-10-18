using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>The Tenancy module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class TenancyErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "TENANT_CONCURRENCY_CONFLICT", "The tenant was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error NotFound = new(
        "TENANT_NOT_FOUND", "No tenant with this ID.", ErrorKind.NotFound);

    public static readonly Error SlugTaken = new(
        "TENANT_SLUG_TAKEN", "This slug belongs to another tenant, possibly an archived one. Slugs are never reused.", ErrorKind.Conflict);

    public static readonly Error InvalidTransition = new(
        "TENANT_INVALID_TRANSITION", "The tenant's status does not allow this change.", ErrorKind.Conflict);
}
