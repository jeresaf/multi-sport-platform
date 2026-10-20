using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Features.Members;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Invitations.RevokeInvitation;

/// <summary>
/// Revokes a pending invitation of the request's tenant: its link stops working. An accepted, revoked or expired one is
/// <c>AUTH_INVITATION_TRANSITION_INVALID</c>; only an owner revokes one that carries <c>TENANT_OWNER</c>
/// (<c>AUTH_OWNER_REQUIRED</c>). An email sent between the load and the save makes this save conflict (409, retry); an
/// email being sent after it conflicts in the outbox and is skipped on retry.
/// </summary>
public sealed class RevokeInvitationHandler(IdentityDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result<InvitationResponse>> HandleAsync(
        Guid invitationId, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        audit.Describe(InvitationEndpoints.EntityType, invitationId);

        var invitation = await context.Set<Invitation>().SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            return IdentityErrors.InvitationNotFound;
        }

        var entry = context.Entry(invitation);
        if (!RowVersion.IsSatisfied(entry.Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch))
        {
            return IdentityErrors.ConcurrencyConflict;
        }

        if (InvitationRules.InvitesOwner(invitation) && !await MemberChanges.IsOwnerOrSystemAsync(context, actor, cancellationToken))
        {
            return IdentityErrors.OwnerRequired;
        }

        var now = timeProvider.GetUtcNow();
        var revoked = invitation.Revoke(actor, now);
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return Result.Success(InvitationResponse.From(invitation, entry.Property<uint>(AuditColumns.Version).CurrentValue, now));
    }
}
