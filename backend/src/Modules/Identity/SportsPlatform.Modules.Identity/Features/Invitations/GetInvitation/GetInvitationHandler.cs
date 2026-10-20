using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Invitations.GetInvitation;

/// <summary>One invitation of the request's tenant; another tenant's is <c>AUTH_INVITATION_NOT_FOUND</c> (query filter and RLS).</summary>
public sealed class GetInvitationHandler(IdentityDbContext context, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result<InvitationResponse>> HandleAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        audit.Describe(InvitationEndpoints.EntityType, invitationId);
        var invitation = await context.Set<Invitation>().AsNoTracking()
            .Where(i => i.Id == invitationId)
            .Select(i => new { Invitation = i, Version = EF.Property<uint>(i, AuditColumns.Version) })
            .SingleOrDefaultAsync(cancellationToken);
        return invitation is null
            ? IdentityErrors.InvitationNotFound
            : Result.Success(InvitationResponse.From(invitation.Invitation, invitation.Version, timeProvider.GetUtcNow()));
    }
}
