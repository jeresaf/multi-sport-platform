namespace SportsPlatform.Modules.Identity.Domain.Invitations;

/// <summary>An invitation's stored status (docs/domain/state-machines.md, "Invitation").</summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
}

/// <summary>
/// An invitation as people see it: the stored status, with <see cref="Expired"/> derived from a pending invitation's
/// <c>expires_at</c> (S03 D12). Never stored.
/// </summary>
public enum InvitationState
{
    Pending,
    Expired,
    Accepted,
    Revoked,
}
