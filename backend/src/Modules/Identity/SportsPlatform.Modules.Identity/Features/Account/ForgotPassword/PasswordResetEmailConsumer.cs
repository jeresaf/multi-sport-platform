using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.Modules.Identity.Contracts.Account;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;

/// <summary>
/// Sends the reset email for a <c>PasswordResetRequested</c> (S03 D13). The token's hash is stored through the
/// consumer's DbContext, which the outbox processor saves after the send: a failed send stores nothing and is retried
/// with a new token, and a send whose save fails leaves a link that doesn't work, replaced by the retry's. The newest
/// token wins either way.
/// </summary>
internal sealed class PasswordResetEmailConsumer(UserManager<User> users, IEmailSender emails, IOptions<AccountOptions> options)
    : IIntegrationEventConsumer<PasswordResetRequested.V1>
{
    public const string Name = "identity.password_reset_email";

    public async Task HandleAsync(PasswordResetRequested.V1 integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // The user may have gone, or changed, since the request: send only to a confirmed account that still exists.
        var user = await users.FindByIdAsync(integrationEvent.UserId.ToString());
        if (user?.Email is null || !await users.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = PasswordResetEmail.Link(options.Value.AdminAppUrl, token);
        await emails.SendAsync(PasswordResetEmail.Create(user.Email, link), cancellationToken);
    }
}
