using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Features.Members.RevokeRole;

/// <summary>
/// Revokes a member's role assignment; the row stays with who revoked it and when. Only an owner revokes
/// <c>TENANT_OWNER</c>, and never from the last active owner (<c>AUTH_LAST_OWNER</c>). Takes effect on the member's next
/// request.
/// </summary>
public sealed class RevokeRoleHandler(MemberChanges changes, ICurrentActor actor, TimeProvider timeProvider)
{
    public Task<Result<MemberResponse>> HandleAsync(
        Guid membershipId, Guid assignmentId, uint? version, IfMatch ifMatch, CancellationToken cancellationToken) =>
        changes.ApplyAsync(
            membershipId,
            reason: null,
            version,
            ifMatch,
            membership => ImpactOf(membership, assignmentId),
            membership => membership.RevokeRole(assignmentId, actor, timeProvider.GetUtcNow()),
            cancellationToken);

    private static OwnerImpact ImpactOf(TenantMembership membership, Guid assignmentId)
    {
        if (membership.ActiveRoles.SingleOrDefault(a => a.Id == assignmentId) is not { } assignment || !MemberChanges.IsOwnerRole(assignment))
        {
            return OwnerImpact.None;
        }

        return membership.Status == MembershipStatus.Active ? OwnerImpact.RemovesOwner : OwnerImpact.ChangesOwner;
    }
}
