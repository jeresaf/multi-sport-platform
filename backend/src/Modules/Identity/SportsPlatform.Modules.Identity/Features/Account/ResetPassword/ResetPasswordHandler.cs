using Microsoft.AspNetCore.Identity;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.PasswordReset;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Account.ResetPassword;

/// <summary>
/// Sets a new password with a reset token (S03 D15). One save: the new password and security stamp (every session of
/// the user ends at its next stamp check), the used token deleted, the lockout cleared (owner, T2b), the audit entry.
/// </summary>
public sealed class ResetPasswordHandler(IdentityDbContext context, UserManager<User> users, AuditScope audit)
{
    /// <summary>Null on success.</summary>
    public async Task<PasswordFailure?> HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = request.Token!;

        var user = PasswordResetTokenProvider.TryReadUserId(token) is { } userId ? await users.FindByIdAsync(userId.ToString()) : null;
        if (user is null)
        {
            return IdentityErrors.ResetTokenInvalid;
        }

        audit.Describe(AccountEndpoints.UserEntityType, user.Id);

        // Identity checks the token first, then the password policy; a refusal changes nothing.
        var reset = await users.ResetPasswordAsync(user, token, request.NewPassword!);
        if (!reset.Succeeded)
        {
            return reset.Errors.All(PasswordFailure.IsPolicyError)
                ? PasswordFailure.Rejected(reset.Errors)
                : IdentityErrors.ResetTokenInvalid;
        }

        await RequireAsync(PasswordResetTokenProvider.ConsumeAsync(users, user));
        await RequireAsync(users.ResetAccessFailedCountAsync(user));
        await RequireAsync(users.SetLockoutEndDateAsync(user, null));

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? (PasswordFailure)saved.Error! : null;
    }

    // The store doesn't save, so these only change tracked entities; a failure is a bug.
    private static async Task RequireAsync(Task<IdentityResult> operation)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Resetting the password failed: {string.Join(", ", result.Errors.Select(e => e.Code))}.");
        }
    }
}
