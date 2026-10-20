using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Features.Members.ChangeMemberStatus;

public enum MemberTransition
{
    Suspend,
    Restore,
    Remove,
}

/// <summary>
/// Moves a membership along its state machine (docs/domain/state-machines.md, "Tenant membership"). Only an owner
/// changes an owner's status; suspending or removing the last active owner is <c>AUTH_LAST_OWNER</c>. A suspended or
/// removed member is refused from their next request on.
/// </summary>
public sealed class ChangeMemberStatusHandler(MemberChanges changes, ICurrentActor actor, TimeProvider timeProvider)
{
    public Task<Result<MemberResponse>> HandleAsync(
        Guid membershipId, MemberTransition transition, string? reason, uint? version, IfMatch ifMatch, CancellationToken cancellationToken) =>
        changes.ApplyAsync(
            membershipId,
            reason,
            version,
            ifMatch,
            membership => ImpactOf(membership, transition),
            membership => transition switch
            {
                MemberTransition.Suspend => membership.Suspend(reason!, actor, timeProvider.GetUtcNow()),
                MemberTransition.Restore => membership.Restore(actor, timeProvider.GetUtcNow()),
                MemberTransition.Remove => membership.Remove(reason!, actor, timeProvider.GetUtcNow()),
                _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
            },
            cancellationToken);

    private static OwnerImpact ImpactOf(TenantMembership membership, MemberTransition transition)
    {
        // A removed membership keeps its role rows but is no one's owner any more: every change to it is the state
        // machine's to refuse (AUTH_MEMBERSHIP_TRANSITION_INVALID), whoever asks.
        if (membership.Status == MembershipStatus.Removed || !MemberChanges.IsOwner(membership))
        {
            return OwnerImpact.None;
        }

        // Restoring adds an active owner; changing a suspended or removed one takes none away.
        return transition != MemberTransition.Restore && membership.Status == MembershipStatus.Active
            ? OwnerImpact.RemovesOwner
            : OwnerImpact.ChangesOwner;
    }
}
