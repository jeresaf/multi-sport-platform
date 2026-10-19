using Microsoft.AspNetCore.Identity;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.PasswordReset;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Account.ChangePassword;

/// <summary>
/// Changes the signed-in user's password (S03 D5, D15). The new security stamp ends the user's other sessions at their
/// next stamp check (within 5 minutes); this session gets a new cookie and stays. A wrong current password counts toward
/// the lockout (owner, T2b) and is saved with its audit entry; a pending reset token is deleted; while locked, every attempt answers the same without a
/// check. A wrong current password is a 400, not a 401: the session is fine.
/// </summary>
public sealed class ChangePasswordHandler(
    IdentityDbContext context,
    UserManager<User> users,
    SignInManager<User> signIn,
    ICurrentActor actor,
    AuditScope audit)
{
    /// <summary>Null on success.</summary>
    public async Task<PasswordFailure?> HandleAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var currentPassword = request.CurrentPassword!;

        var user = await users.FindByIdAsync(actor.ActorId.ToString());
        if (user is null)
        {
            // A session for a user that no longer exists.
            return AuthorizationErrors.Unauthenticated;
        }

        audit.Describe(AccountEndpoints.UserEntityType, user.Id);

        if (users.SupportsUserLockout && await users.IsLockedOutAsync(user))
        {
            // Hashed anyway, so a locked account answers as slowly as a wrong password.
            _ = users.PasswordHasher.HashPassword(user, currentPassword);
            return IdentityErrors.CurrentPasswordInvalid;
        }

        // Identity checks the current password first, then the new one against the policy.
        var change = await users.ChangePasswordAsync(user, currentPassword, request.NewPassword!);
        if (!change.Succeeded)
        {
            if (!change.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                return change.Errors.All(PasswordFailure.IsPolicyError)
                    ? PasswordFailure.Rejected(change.Errors)
                    : throw new InvalidOperationException(
                        $"Changing the password failed: {string.Join(", ", change.Errors.Select(e => e.Code))}.");
            }

            _ = await users.AccessFailedAsync(user);
            audit.RecordOnSave(AuditOutcome.Failure, IdentityErrors.CurrentPasswordInvalid);
            var counted = await context.SaveChangesResultAsync(cancellationToken);
            return counted.IsFailure ? counted.Error! : IdentityErrors.CurrentPasswordInvalid;
        }

        _ = await users.ResetAccessFailedCountAsync(user);

        // A pending reset link would set a password behind the new one: it ends with the change.
        _ = await PasswordResetTokenProvider.ConsumeAsync(users, user);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error!;
        }

        // A new cookie with the new stamp; the session keeps its start (issued_at), so the 7-day limit still counts from sign-in.
        await signIn.RefreshSignInAsync(user);
        return null;
    }
}
