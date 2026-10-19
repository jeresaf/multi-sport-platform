using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy;

/// <summary>The Tenancy module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class TenancyErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "TENANT_CONCURRENCY_CONFLICT", "The tenant was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error NotFound = new(
        "TENANT_NOT_FOUND", "No such tenant.", ErrorKind.NotFound);

    public static readonly Error SlugTaken = new(
        "TENANT_SLUG_TAKEN", "This slug belongs to another tenant, possibly an archived one. Slugs are never reused.", ErrorKind.Conflict);

    /// <summary>Access mode BLOCKED (suspended or archived): admin requests are refused (S02 D9).</summary>
    public static readonly Error Blocked = new(
        "TENANT_BLOCKED", "This tenant is suspended or archived.", ErrorKind.Forbidden);

    /// <summary>A public request for a tenant whose site is hidden or off; says nothing more (S02-T3).</summary>
    public static readonly Error Unavailable = new(
        "TENANT_UNAVAILABLE", "This site is not available.", ErrorKind.Forbidden);

    public static readonly Error InvalidTransition = new(
        "TENANT_INVALID_TRANSITION", "The tenant's status does not allow this change.", ErrorKind.Conflict);
}
