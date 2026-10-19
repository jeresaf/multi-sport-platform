using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Authorization failures shared by every module.</summary>
public static class AuthorizationErrors
{
    /// <summary>
    /// The caller is not signed in (or the session has no valid user ID) and the action needs a user (S03 D4).
    /// </summary>
    public static readonly Error Unauthenticated = new(
        "AUTH_UNAUTHENTICATED", "Sign in to perform this action.", ErrorKind.Unauthorized);

    /// <summary>
    /// A state-changing request to <c>/api/account</c>, <c>/api/admin</c> or <c>/api/platform</c> without <c>X-CSRF: 1</c>
    /// (S03 D7).
    /// </summary>
    public static readonly Error CsrfRequired = new(
        "AUTH_CSRF_REQUIRED", "The request must carry the X-CSRF header.", ErrorKind.Forbidden);

    public static readonly Error Forbidden = new(
        "AUTH_FORBIDDEN", "You are not allowed to perform this action.", ErrorKind.Forbidden);

    /// <summary>The admin request names no tenant, or one the actor is not a member of.</summary>
    public static readonly Error TenantForbidden = new(
        "AUTH_TENANT_FORBIDDEN", "You are not allowed to work in this tenant.", ErrorKind.Forbidden);
}
