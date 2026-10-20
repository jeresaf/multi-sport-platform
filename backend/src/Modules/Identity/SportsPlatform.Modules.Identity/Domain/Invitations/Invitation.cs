using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.Modules.Identity.Domain.Invitations;

/// <summary>
/// An invitation to join a tenant with roles (<c>identity.invitations</c>, tenant-owned, S03 D8, D12). Status:
/// <c>PENDING → ACCEPTED | REVOKED</c>; <c>EXPIRED</c> is derived from <see cref="ExpiresAt"/>, 7 days after it was made,
/// and is final. The token is made when the email is sent (D13): only its SHA-256 is stored, and a newer one replaces
/// it. Roles are tenant-wide in S03.
/// </summary>
public sealed class Invitation : AggregateRoot, ITenantOwned
{
    public const int EmailMaxLength = 256;

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly List<string> _roleCodes = [];

    private Invitation()
    {
        Email = null!;
        NormalizedEmail = null!;
    }

    private Invitation(string email, string normalizedEmail, IEnumerable<string> roleCodes, Guid invitedBy, DateTimeOffset now)
        : base(NewId())
    {
        Email = email;
        NormalizedEmail = normalizedEmail;
        _roleCodes.AddRange(roleCodes);
        Status = InvitationStatus.Pending;
        InvitedBy = invitedBy;
        InvitedAt = now;
        ExpiresAt = now.Add(Lifetime);
    }

    public Guid TenantId { get; private set; }

    /// <summary>The invitee's email as the inviter wrote it (PRIVATE, ADR-0015).</summary>
    public string Email { get; private set; }

    /// <summary>The email as Identity normalises it, to match a user and a pending invitation.</summary>
    public string NormalizedEmail { get; private set; }

    /// <summary>The tenant roles the invitee gets, tenant-wide, in the order given.</summary>
    public IReadOnlyList<string> RoleCodes => _roleCodes;

    public InvitationStatus Status { get; private set; }

    public Guid InvitedBy { get; private set; }

    public DateTimeOffset InvitedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The SHA-256 of the newest emailed token; null until the email is sent.</summary>
    public byte[]? TokenHash { get; private set; }

    public DateTimeOffset? TokenIssuedAt { get; private set; }

    public Guid? RevokedBy { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The user who accepted: created by the acceptance, or signed in as the invited email.</summary>
    public Guid? AcceptedBy { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>The membership the acceptance created (same tenant).</summary>
    public Guid? MembershipId { get; private set; }

    /// <summary>
    /// A pending invitation for the scope's tenant (stamped on save): <c>AUTH_ROLE_NOT_ASSIGNABLE</c> unless there is at
    /// least one role, each a tenant role assignable tenant-wide; a role named twice counts once.
    /// </summary>
    public static Result<Invitation> Create(
        string email, string normalizedEmail, IReadOnlyCollection<RoleDefinition> roles, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(actor);
        if (roles.Count == 0 || roles.Any(role => !RoleAssignment.IsAssignable(role, AssignmentScope.Tenant, scopeId: null)))
        {
            return IdentityErrors.RoleNotAssignable;
        }

        var codes = roles.Select(role => role.Code).Distinct(StringComparer.Ordinal);
        return Result.Success(new Invitation(email, normalizedEmail, codes, actor.ActorId, now));
    }

    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    public InvitationState StateAt(DateTimeOffset now) => Status switch
    {
        InvitationStatus.Pending => IsExpiredAt(now) ? InvitationState.Expired : InvitationState.Pending,
        InvitationStatus.Accepted => InvitationState.Accepted,
        InvitationStatus.Revoked => InvitationState.Revoked,
        _ => throw new InvalidOperationException($"Unknown invitation status {Status}."),
    };

    /// <summary>
    /// Whether the invitation's token still works (S03-T4c2): pending and not expired. Otherwise
    /// <c>AUTH_INVITATION_EXPIRED</c>, <c>AUTH_INVITATION_ALREADY_ACCEPTED</c>, or <c>AUTH_INVITATION_TOKEN_INVALID</c> for a
    /// revoked one (revoked and unknown tokens answer alike).
    /// </summary>
    public Result CheckUsableAt(DateTimeOffset now) => StateAt(now) switch
    {
        InvitationState.Pending => Result.Success(),
        InvitationState.Expired => Result.Failure(IdentityErrors.InvitationExpired),
        InvitationState.Accepted => Result.Failure(IdentityErrors.InvitationAlreadyAccepted),
        _ => Result.Failure(IdentityErrors.InvitationTokenInvalid),
    };

    /// <summary>
    /// <c>PENDING</c> → <c>ACCEPTED</c> by <paramref name="userId"/>, who joined as <paramref name="membershipId"/>: the
    /// token works once. Refused as <see cref="CheckUsableAt"/> says.
    /// </summary>
    public Result Accept(Guid userId, Guid membershipId, DateTimeOffset now)
    {
        if (userId == Guid.Empty || membershipId == Guid.Empty)
        {
            throw new ArgumentException("An accepted invitation names its user and membership.");
        }

        var usable = CheckUsableAt(now);
        if (usable.IsFailure)
        {
            return usable;
        }

        Status = InvitationStatus.Accepted;
        AcceptedBy = userId;
        AcceptedAt = now;
        MembershipId = membershipId;
        return Result.Success();
    }

    /// <summary><c>PENDING</c> → <c>REVOKED</c>; an expired, accepted or revoked one is <c>AUTH_INVITATION_TRANSITION_INVALID</c>.</summary>
    public Result Revoke(ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (StateAt(now) != InvitationState.Pending)
        {
            return Result.Failure(IdentityErrors.InvitationTransitionInvalid);
        }

        MarkRevoked(actor, now);
        return Result.Success();
    }

    /// <summary>
    /// A new invitation to the same email replaces this one (S03 D12): a pending one, expired or not, is revoked, so its
    /// link stops working and at most one invitation per email is pending.
    /// </summary>
    public void Supersede(ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation is replaced.");
        }

        MarkRevoked(actor, now);
    }

    /// <summary>
    /// Stores the hash of a newly emailed token, replacing any earlier one: false (nothing changes) unless the
    /// invitation is pending and not expired, so no email goes out for it.
    /// </summary>
    public bool IssueToken(byte[] tokenHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (tokenHash.Length != 32)
        {
            throw new ArgumentException("A token hash is a SHA-256 (32 bytes).", nameof(tokenHash));
        }

        if (StateAt(now) != InvitationState.Pending)
        {
            return false;
        }

        TokenHash = tokenHash;
        TokenIssuedAt = now;
        return true;
    }

    private void MarkRevoked(ICurrentActor actor, DateTimeOffset now)
    {
        Status = InvitationStatus.Revoked;
        RevokedBy = actor.ActorId;
        RevokedAt = now;
    }
}
