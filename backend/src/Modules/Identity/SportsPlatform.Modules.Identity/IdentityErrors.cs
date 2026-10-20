using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity;

/// <summary>The Identity module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class IdentityErrors
{
    /// <summary>
    /// Sign-in refused (S03 D15): an unknown email, a wrong password, a locked or unconfirmed account all answer this,
    /// so the answer never tells whether an account exists. The audit entry records the real reason.
    /// </summary>
    public static readonly Error InvalidCredentials = new(
        "AUTH_INVALID_CREDENTIALS", "The email or password is incorrect, or the account can't sign in right now.", ErrorKind.Unauthorized);

    public static readonly Error ConcurrencyConflict = new(
        "AUTH_CONCURRENCY_CONFLICT", "The account was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    /// <summary>
    /// The password fails the policy (S03 D15: at least 12 characters, no composition rules). On the account endpoints,
    /// <c>errors</c> names the field and <c>detail</c> the rule.
    /// </summary>
    public static readonly Error PasswordRejected = new(
        "AUTH_PASSWORD_REJECTED", "The password was rejected: use at least 12 characters.", ErrorKind.Validation);

    /// <summary>
    /// Password change with a wrong current password. A 400, not a 401: the session is fine, and a 401 would read as a
    /// lost one. Counts toward the lockout like a wrong password at sign-in.
    /// </summary>
    public static readonly Error CurrentPasswordInvalid = new(
        "AUTH_CURRENT_PASSWORD_INVALID", "The current password is incorrect.", ErrorKind.Validation);

    /// <summary>A reset token that is malformed, unknown, expired, already used or replaced by a newer one.</summary>
    public static readonly Error ResetTokenInvalid = new(
        "AUTH_RESET_TOKEN_INVALID", "The reset link is invalid or has expired. Ask for a new one.", ErrorKind.Validation);

    /// <summary>
    /// Too many sign-in or password-reset requests from this address or for this email (S03 D15). <c>Retry-After</c>
    /// says when to try again.
    /// </summary>
    public static readonly Error RateLimited = new(
        "AUTH_RATE_LIMITED", "Too many attempts. Wait a while and try again.", ErrorKind.TooManyRequests);

    /// <summary>Creating a user whose email another user has (CLI <c>create-platform-admin</c>).</summary>
    public static readonly Error UserExists = new(
        "AUTH_USER_EXISTS", "A user with this email already exists.", ErrorKind.Conflict);

    /// <summary>
    /// A membership change its status doesn't allow (docs/domain/state-machines.md, "Tenant membership"), such as
    /// restoring an active member, or any change to a removed one.
    /// </summary>
    public static readonly Error MembershipTransitionInvalid = new(
        "AUTH_MEMBERSHIP_TRANSITION_INVALID", "The membership's status doesn't allow this change.", ErrorKind.Conflict);

    /// <summary>
    /// The role can't be held there: not a tenant role, not assignable at that scope (authorization.md, "typical
    /// scope"), or a scope without its resource.
    /// </summary>
    public static readonly Error RoleNotAssignable = new(
        "AUTH_ROLE_NOT_ASSIGNABLE", "This role can't be assigned at this scope.", ErrorKind.Validation);

    public static readonly Error RoleAlreadyAssigned = new(
        "AUTH_ROLE_ALREADY_ASSIGNED", "The member already holds this role at this scope.", ErrorKind.Conflict);

    /// <summary>No active role assignment with this ID on the membership (unknown or already revoked).</summary>
    public static readonly Error RoleAssignmentNotFound = new(
        "AUTH_ROLE_ASSIGNMENT_NOT_FOUND", "The role assignment was not found.", ErrorKind.NotFound);
}
