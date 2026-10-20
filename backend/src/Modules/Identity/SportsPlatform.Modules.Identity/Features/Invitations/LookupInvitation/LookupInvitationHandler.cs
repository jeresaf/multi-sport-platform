using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Features.Invitations.Acceptance;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Features.Invitations.LookupInvitation;

/// <summary>
/// <c>invitation.read</c> (S03-T4c2): what an invitation link is for, refused as accepting it would be (unknown,
/// revoked, expired or accepted token; archived tenant). Changes nothing.
/// </summary>
public sealed class LookupInvitationHandler(
    InvitationByToken invitations, IdentityDbContext context, ITenantDirectory tenants, TimeProvider timeProvider)
{
    public async Task<Result<LookupInvitationResponse>> HandleAsync(LookupInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await invitations.FindAsync(request.Token!, cancellationToken);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var invitation = found.Value;
        var usable = invitation.CheckUsableAt(timeProvider.GetUtcNow());
        if (usable.IsFailure)
        {
            return usable.Error!;
        }

        var tenant = await tenants.GetByIdAsync(invitation.TenantId, cancellationToken);
        if (tenant.IsFailure)
        {
            return tenant.Error;
        }

        if (tenant.Value.Status == TenantStatus.Archived)
        {
            return IdentityErrors.TenantArchived;
        }

        var accountExists = await context.Set<User>().AnyAsync(u => u.NormalizedEmail == invitation.NormalizedEmail, cancellationToken);
        return Result.Success(new LookupInvitationResponse(
            tenant.Value.DisplayName, invitation.Email, invitation.RoleCodes, invitation.ExpiresAt, accountExists));
    }
}
