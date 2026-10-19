using Microsoft.AspNetCore.Identity;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Identity.Contracts.Account;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;

/// <summary>Why no reset email goes out: in the audit entry's metadata only, never in the answer (S03 D15).</summary>
public enum ForgotPasswordIgnoredReason
{
    UnknownEmail,

    /// <summary>The email is not confirmed: the account can't sign in, so it gets no reset link either.</summary>
    NotConfirmed,
}

/// <summary>The audit metadata of a request that sends nothing. Never the email.</summary>
public sealed record ForgotPasswordIgnored(ForgotPasswordIgnoredReason Reason);

/// <summary>
/// Asks for a password reset email (S03 D13, D15). The caller always gets the same answer. For a confirmed user, the
/// request publishes <c>PasswordResetRequested</c> in the same save as its audit entry; the consumer creates the token and
/// sends the email, so a rolled-back request sends nothing.
/// </summary>
public sealed class ForgotPasswordHandler(IdentityDbContext context, UserManager<User> users, AuditScope audit)
{
    public async Task HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !await users.IsEmailConfirmedAsync(user))
        {
            var reason = user is null ? ForgotPasswordIgnoredReason.UnknownEmail : ForgotPasswordIgnoredReason.NotConfirmed;
            audit.Describe(AccountEndpoints.UserEntityType, user?.Id, metadata: new ForgotPasswordIgnored(reason));
            return;
        }

        audit.Describe(AccountEndpoints.UserEntityType, user.Id);
        context.Publish(new PasswordResetRequested.V1(user.Id), AccountEndpoints.UserEntityType, user.Id);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            // Only an outbox row and the audit entry are inserted: nothing can conflict, so this is a bug.
            throw new InvalidOperationException($"Saving the password reset request failed: {saved.Error!.Code}.");
        }
    }
}
