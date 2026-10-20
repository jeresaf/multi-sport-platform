using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Members.ChangeMemberStatus;

public enum MemberTransition
{
    Suspend,
    Restore,
    Remove,
}

/// <summary>The invitations a suspension or removal revoked (Q20): IDs only, never the emails.</summary>
public sealed record MemberStatusChangedAuditMetadata(IReadOnlyList<Guid> RevokedInvitationIds);

/// <summary>
/// Moves a membership along its state machine (docs/domain/state-machines.md, "Tenant membership"). Only an owner
/// changes an owner's status; suspending or removing the last active owner is <c>AUTH_LAST_OWNER</c>. A suspended or
/// removed member is refused from their next request on. Suspending or removing also revokes the pending, unexpired
/// invitations that member's user sent in the tenant, in the same save (Q20), platform owner invitations included;
/// restoring doesn't bring them back. An invitation that changes between the load and the save (its email being sent)
/// makes the member change conflict (409 AUTH_CONCURRENCY_CONFLICT); a retry succeeds.
/// </summary>
public sealed class ChangeMemberStatusHandler(
    MemberChanges changes, IdentityDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result<MemberResponse>> HandleAsync(
        Guid membershipId, MemberTransition transition, string? reason, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        List<Invitation> sent = transition == MemberTransition.Restore
            ? []
            : await PendingInvitationsSentByAsync(membershipId, now, cancellationToken);

        return await changes.ApplyAsync(
            membershipId,
            reason,
            version,
            ifMatch,
            membership => ImpactOf(membership, transition),
            membership => Change(membership, transition, reason, sent, now),
            cancellationToken);
    }

    private Result Change(TenantMembership membership, MemberTransition transition, string? reason, List<Invitation> sent, DateTimeOffset now)
    {
        var changed = transition switch
        {
            MemberTransition.Suspend => membership.Suspend(reason!, actor, now),
            MemberTransition.Restore => membership.Restore(actor, now),
            MemberTransition.Remove => membership.Remove(reason!, actor, now),
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
        };
        if (changed.IsFailure || transition == MemberTransition.Restore)
        {
            return changed;
        }

        foreach (var invitation in sent)
        {
            if (invitation.Revoke(actor, now).IsFailure)
            {
                throw new InvalidOperationException($"Invitation {invitation.Id} was loaded as pending and unexpired.");
            }
        }

        audit.Describe(metadata: new MemberStatusChangedAuditMetadata([.. sent.Select(invitation => invitation.Id)]));
        return changed;
    }

    /// <summary>The request's tenant's (RLS) pending, unexpired invitations sent by the membership's user, tracked.</summary>
    private Task<List<Invitation>> PendingInvitationsSentByAsync(Guid membershipId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var inviter = context.Set<TenantMembership>().Where(m => m.Id == membershipId).Select(m => m.UserId);
        return context.Set<Invitation>()
            .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt > now && inviter.Contains(i.InvitedBy))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
    }

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
