using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.Modules.Identity.Domain.Memberships;

/// <summary>
/// A user's membership of a tenant (<c>identity.tenant_memberships</c>, tenant-owned, S03 D8) and the roles it holds.
/// Status: <c>ACTIVE ⇄ SUSPENDED</c>, either → <c>REMOVED</c> (terminal; rejoining is a new membership). Only an active
/// membership's unrevoked roles grant actions, read on every request (S03 D5). Role changes on a removed membership
/// are refused. The last-owner rule (S03 D17) spans memberships, so the handlers check it.
/// </summary>
public sealed class TenantMembership : AggregateRoot, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    private readonly List<RoleAssignment> _roles = [];

    private TenantMembership()
    {
    }

    private TenantMembership(Guid userId, Guid? invitedBy, DateTimeOffset joinedAt)
        : base(NewId())
    {
        UserId = userId;
        InvitedBy = invitedBy;
        JoinedAt = joinedAt;
        Status = MembershipStatus.Active;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    /// <summary>Who sent the invitation the user accepted; null when the membership was not created from one.</summary>
    public Guid? InvitedBy { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>The reason of the last suspension or removal; cleared on restore. The audit trail keeps every change.</summary>
    public string? StatusReason { get; private set; }

    public Guid? StatusChangedBy { get; private set; }

    public DateTimeOffset? StatusChangedAt { get; private set; }

    /// <summary>
    /// When a role was last assigned or revoked. Changing it changes the membership row, so a concurrent change to the
    /// same membership's roles fails its version check (only the root's <c>xmin</c> is checked, database.md).
    /// </summary>
    public DateTimeOffset? RolesChangedAt { get; private set; }

    /// <summary>Every assignment, revoked ones included.</summary>
    public IReadOnlyList<RoleAssignment> Roles => _roles;

    public IEnumerable<RoleAssignment> ActiveRoles => _roles.Where(role => role.IsActive);

    /// <summary>The user joins the scope's tenant (stamped on save) as an active member without roles.</summary>
    public static TenantMembership Join(Guid userId, Guid? invitedBy, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A membership belongs to a user.", nameof(userId));
        }

        return new TenantMembership(userId, invitedBy, now);
    }

    /// <summary>
    /// Grants <paramref name="role"/> at a scope: <c>AUTH_ROLE_NOT_ASSIGNABLE</c> unless it is a tenant role assignable
    /// there, <c>AUTH_ROLE_ALREADY_ASSIGNED</c> when held there already, <c>AUTH_MEMBERSHIP_TRANSITION_INVALID</c> once
    /// removed.
    /// </summary>
    public Result<RoleAssignment> AssignRole(
        RoleDefinition role, AssignmentScope scopeType, Guid? scopeId, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(actor);
        if (Status == MembershipStatus.Removed)
        {
            return IdentityErrors.MembershipTransitionInvalid;
        }

        if (!RoleAssignment.IsAssignable(role, scopeType, scopeId))
        {
            return IdentityErrors.RoleNotAssignable;
        }

        if (ActiveRoles.Any(held => held.IsSameAs(role.Code, scopeType, scopeId)))
        {
            return IdentityErrors.RoleAlreadyAssigned;
        }

        var assignment = RoleAssignment.Grant(Id, role, scopeType, scopeId, actor, now);
        _roles.Add(assignment);
        RolesChangedAt = now;
        return Result.Success(assignment);
    }

    /// <summary>Revokes an active assignment: <c>AUTH_ROLE_ASSIGNMENT_NOT_FOUND</c> for an unknown or revoked one.</summary>
    public Result RevokeRole(Guid assignmentId, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Status == MembershipStatus.Removed)
        {
            return Result.Failure(IdentityErrors.MembershipTransitionInvalid);
        }

        if (ActiveRoles.SingleOrDefault(role => role.Id == assignmentId) is not { } assignment)
        {
            return Result.Failure(IdentityErrors.RoleAssignmentNotFound);
        }

        assignment.Revoke(actor, now);
        RolesChangedAt = now;
        return Result.Success();
    }

    /// <summary><c>ACTIVE</c> → <c>SUSPENDED</c>, with a reason.</summary>
    public Result Suspend(string reason, ICurrentActor actor, DateTimeOffset now) =>
        Transition(MembershipStatus.Suspended, [MembershipStatus.Active], RequireReason(reason), actor, now);

    /// <summary><c>SUSPENDED</c> → <c>ACTIVE</c>.</summary>
    public Result Restore(ICurrentActor actor, DateTimeOffset now) =>
        Transition(MembershipStatus.Active, [MembershipStatus.Suspended], reason: null, actor, now);

    /// <summary><c>ACTIVE</c> or <c>SUSPENDED</c> → <c>REMOVED</c>, with a reason.</summary>
    public Result Remove(string reason, ICurrentActor actor, DateTimeOffset now) =>
        Transition(MembershipStatus.Removed, [MembershipStatus.Active, MembershipStatus.Suspended], RequireReason(reason), actor, now);

    private Result Transition(MembershipStatus to, MembershipStatus[] from, string? reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!from.Contains(Status))
        {
            return Result.Failure(IdentityErrors.MembershipTransitionInvalid);
        }

        Status = to;
        StatusReason = reason;
        StatusChangedBy = actor.ActorId;
        StatusChangedAt = now;
        return Result.Success();
    }

    private static string RequireReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmed = reason.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, ReasonMaxLength, nameof(reason));
        return trimmed;
    }
}
