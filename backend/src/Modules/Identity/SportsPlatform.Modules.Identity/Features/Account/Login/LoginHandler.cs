using Microsoft.AspNetCore.Identity;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Sessions;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Account.Login;

/// <summary>
/// Signs in with email and password (S03 D15). Unknown email, wrong password, a locked or unconfirmed account all answer
/// <c>AUTH_INVALID_CREDENTIALS</c>; the audit entry records the real reason. Five wrong passwords lock the account for
/// 15 minutes. The store doesn't save (UserStore), so the failed-attempt count is saved here, with the DENIED entry.
/// </summary>
public sealed class LoginHandler(
    IdentityDbContext context,
    UserManager<User> users,
    SignInManager<User> signIn,
    AuditScope audit,
    TimeProvider timeProvider)
{
    // A save that loses a race with another sign-in of the same user is retried, so no failed attempt goes uncounted.
    private const int _saveAttempts = 3;

    // Hashed in place of an unknown user's password, so the answer takes as long as for a real account.
    private static readonly User _nobody = User.Create("nobody@invalid.example", emailConfirmed: false);

    public async Task<Result> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var password = request.Password!;

        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
        {
            _ = users.PasswordHasher.HashPassword(_nobody, password);
            audit.Describe(AccountEndpoints.UserEntityType, metadata: new LoginFailure(LoginFailureReason.UnknownEmail));
            return Result.Failure(IdentityErrors.InvalidCredentials);
        }

        audit.Describe(AccountEndpoints.UserEntityType, user.Id);
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            // Saves the reset failed-attempt count (and commits the entry) before the cookie is issued.
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved;
            }

            await signIn.SignInAsync(user, SessionCookie.NewSession(timeProvider.GetUtcNow()), authenticationMethod: null);
            return Result.Success();
        }

        var reason = result.IsLockedOut ? LoginFailureReason.LockedOut
            : result.IsNotAllowed ? LoginFailureReason.NotAllowed
            : LoginFailureReason.WrongPassword;
        audit.Describe(metadata: new LoginFailure(reason));
        audit.RecordOnSave(AuditOutcome.Denied, IdentityErrors.InvalidCredentials);
        await SaveFailedAttemptAsync(user, reason, cancellationToken);
        return Result.Failure(IdentityErrors.InvalidCredentials);
    }

    private async Task SaveFailedAttemptAsync(User user, LoginFailureReason reason, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsSuccess || saved.Error != IdentityErrors.ConcurrencyConflict || attempt == _saveAttempts)
            {
                // A save that still fails leaves the entry to the audit middleware (DENIED, from the 401).
                return;
            }

            // Another request changed the user: count this attempt again on the current row.
            await context.Entry(user).ReloadAsync(cancellationToken);
            if (reason == LoginFailureReason.WrongPassword)
            {
                _ = await users.AccessFailedAsync(user);
            }
        }
    }
}
