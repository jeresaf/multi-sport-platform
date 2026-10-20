using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.Modules.Identity.Contracts.Invitations;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Invitations;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Features.Invitations.SendInvitationEmail;

/// <summary>
/// Sends the email for an <c>InvitationIssued</c> (S03 D13), under the event's tenant. The token's hash is stored on the
/// invitation through the consumer's DbContext, which the outbox processor saves after the send: a failed send stores
/// nothing and is retried with a new token, and a send whose save fails (the invitation was revoked meanwhile) leaves a
/// link that doesn't work. The newest token wins either way. An invitation that is no longer pending, or has expired,
/// gets no email.
/// </summary>
internal sealed class InvitationEmailConsumer(
    IdentityDbContext context,
    ITenantDirectory tenants,
    IEmailSender emails,
    IOptions<AccountOptions> options,
    TimeProvider timeProvider) : IIntegrationEventConsumer<InvitationIssued.V1>
{
    public const string Name = "identity.invitation_email";

    public async Task HandleAsync(InvitationIssued.V1 integrationEvent, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var invitation = await context.Set<Invitation>().SingleOrDefaultAsync(i => i.Id == integrationEvent.InvitationId, cancellationToken);
        var (token, hash) = InvitationToken.Create();
        if (invitation is null || !invitation.IssueToken(hash, timeProvider.GetUtcNow()))
        {
            return;
        }

        var tenant = await tenants.GetByIdAsync(invitation.TenantId, cancellationToken);
        if (tenant.IsFailure)
        {
            // Tenants are never deleted (history over overwrite): an invitation's tenant always exists.
            throw new InvalidOperationException($"The tenant {invitation.TenantId} of invitation {invitation.Id} was not found.");
        }

        var link = InvitationEmail.Link(options.Value.AdminAppUrl, token);
        await emails.SendAsync(InvitationEmail.Create(invitation.Email, tenant.Value.DisplayName, link), cancellationToken);
    }
}
