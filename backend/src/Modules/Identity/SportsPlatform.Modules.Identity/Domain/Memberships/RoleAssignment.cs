using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.Modules.Identity.Domain.Memberships;

/// <summary>
/// A tenant role held by a membership at a scope (docs/architecture/authorization.md, "Roles and scopes"): the whole
/// tenant (<see cref="ScopeId"/> null) or one competition, team or contest. Tenant-owned; revoking keeps the row with
/// who revoked it and when. Granted through <see cref="TenantMembership.AssignRole"/>.
/// </summary>
public sealed class RoleAssignment : Entity, ITenantOwned
{
    public const int RoleCodeMaxLength = 50;

    private RoleAssignment()
    {
        RoleCode = null!;
    }

    private RoleAssignment(Guid membershipId, string roleCode, AssignmentScope scopeType, Guid? scopeId, Guid grantedBy, DateTimeOffset grantedAt)
        : base(NewId())
    {
        MembershipId = membershipId;
        RoleCode = roleCode;
        ScopeType = scopeType;
        ScopeId = scopeId;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt;
    }

    public Guid TenantId { get; private set; }

    public Guid MembershipId { get; private set; }

    /// <summary>A tenant role's code, e.g. <c>TENANT_ADMIN</c> (<see cref="IdentityRoles"/>).</summary>
    public string RoleCode { get; private set; }

    public AssignmentScope ScopeType { get; private set; }

    /// <summary>The competition, team or contest; null for <see cref="AssignmentScope.Tenant"/> (the membership's tenant).</summary>
    public Guid? ScopeId { get; private set; }

    public Guid GrantedBy { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public Guid? RevokedBy { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    /// <summary>Whether the role can be held at this scope: a tenant role, one of its assignment scopes, an ID unless tenant-wide.</summary>
    public static bool IsAssignable(RoleDefinition role, AssignmentScope scopeType, Guid? scopeId)
    {
        ArgumentNullException.ThrowIfNull(role);
        return role.Scope == RoleScope.Tenant
            && role.AssignableAt.Contains(scopeType)
            && (scopeType == AssignmentScope.Tenant ? scopeId is null : scopeId is { } id && id != Guid.Empty);
    }

    /// <summary>
    /// A new assignment for <paramref name="membershipId"/>. Use <see cref="TenantMembership.AssignRole"/>, which checks
    /// the membership; this is public for the isolation harness, which points one at another tenant's membership.
    /// </summary>
    public static RoleAssignment Grant(
        Guid membershipId, RoleDefinition role, AssignmentScope scopeType, Guid? scopeId, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(actor);
        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("A role is assigned to a membership.", nameof(membershipId));
        }

        if (!IsAssignable(role, scopeType, scopeId))
        {
            throw new ArgumentException($"'{role.Code}' cannot be assigned at {scopeType} {scopeId}.", nameof(scopeType));
        }

        return new RoleAssignment(membershipId, role.Code, scopeType, scopeId, actor.ActorId, now);
    }

    /// <summary>The same role at the same scope (an active one is held at most once).</summary>
    public bool IsSameAs(string roleCode, AssignmentScope scopeType, Guid? scopeId) =>
        RoleCode == roleCode && ScopeType == scopeType && ScopeId == scopeId;

    internal void Revoke(ICurrentActor actor, DateTimeOffset now)
    {
        RevokedBy = actor.ActorId;
        RevokedAt = now;
    }
}
