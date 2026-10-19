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

    /// <summary>The password fails the policy (S03 D15: at least 12 characters, no composition rules).</summary>
    public static readonly Error PasswordRejected = new(
        "AUTH_PASSWORD_REJECTED", "The password was rejected: use at least 12 characters.", ErrorKind.Validation);

    /// <summary>Creating a user whose email another user has (CLI <c>create-platform-admin</c>).</summary>
    public static readonly Error UserExists = new(
        "AUTH_USER_EXISTS", "A user with this email already exists.", ErrorKind.Conflict);
}
