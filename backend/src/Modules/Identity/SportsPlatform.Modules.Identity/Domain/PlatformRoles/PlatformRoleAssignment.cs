using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Identity.Domain.PlatformRoles;

/// <summary>
/// A platform role held by a user (global: platform staff have no tenant, S03 D8). Revoking keeps the row with who
/// revoked it and when; a user holds a role at most once at a time.
/// </summary>
public sealed class PlatformRoleAssignment : AggregateRoot
{
    public const int RoleCodeMaxLength = 50;

    private PlatformRoleAssignment()
    {
        RoleCode = null!;
    }

    private PlatformRoleAssignment(Guid userId, string roleCode, Guid grantedBy, DateTimeOffset grantedAt)
        : base(NewId())
    {
        UserId = userId;
        RoleCode = roleCode;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt;
    }

    public Guid UserId { get; private set; }

    /// <summary>One of <see cref="PlatformRoles"/>.</summary>
    public string RoleCode { get; private set; }

    public Guid GrantedBy { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public Guid? RevokedBy { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static PlatformRoleAssignment Grant(Guid userId, string roleCode, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A role is granted to a user.", nameof(userId));
        }

        if (!PlatformRoles.All.Contains(roleCode))
        {
            throw new ArgumentException($"'{roleCode}' is not a platform role.", nameof(roleCode));
        }

        return new PlatformRoleAssignment(userId, roleCode, actor.ActorId, now);
    }
}
