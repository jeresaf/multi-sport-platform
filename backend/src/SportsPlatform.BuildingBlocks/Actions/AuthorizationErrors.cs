using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Authorization failures shared by every module.</summary>
public static class AuthorizationErrors
{
    public static readonly Error Forbidden = new(
        "AUTH_FORBIDDEN", "You are not allowed to perform this action.", ErrorKind.Forbidden);

    /// <summary>The admin request names no tenant, or one the actor is not a member of.</summary>
    public static readonly Error TenantForbidden = new(
        "AUTH_TENANT_FORBIDDEN", "You are not allowed to work in this tenant.", ErrorKind.Forbidden);
}
