using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Reads the roles an actor holds, per request (no claims cache, S03 D5), for the evaluator and the capabilities
/// (S03-T5): both decide through <see cref="HeldRoles.Allows"/>, so the capabilities list is what the evaluator allows.
/// </summary>
public sealed class Permissions(IdentityDbContext context, RoleCatalogue roles, ITenantContext tenantContext)
{
    /// <summary>The user's unrevoked platform roles; none for any other actor.</summary>
    public async Task<HeldRoles> PlatformRolesAsync(ICurrentActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.User)
        {
            return HeldRoles.None;
        }

        var codes = await context.Set<PlatformRoleAssignment>().AsNoTracking()
            .Where(a => a.UserId == actor.ActorId && a.RevokedAt == null)
            .Select(a => a.RoleCode)
            .ToListAsync(cancellationToken);
        return new HeldRoles([.. codes
            .Select(roles.Find)
            .OfType<RoleDefinition>()
            .Where(role => role.Scope == RoleScope.Platform)
            .Select(role => new HeldRole(role, AssignmentScope.Tenant, null))]);
    }

    /// <summary>
    /// The user's unrevoked assignments in <paramref name="tenantId"/>, through an active membership; none for any other
    /// actor or when the tenant isn't the scope's (the roles are read under RLS, so tenant resolution sets both).
    /// </summary>
    public async Task<HeldRoles> TenantRolesAsync(ICurrentActor actor, Guid? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.User || tenantId is not { } tenant || tenantContext.TenantId != tenant)
        {
            return HeldRoles.None;
        }

        var held = await context.Set<TenantMembership>().AsNoTracking()
            .Where(m => m.TenantId == tenant && m.UserId == actor.ActorId && m.Status == MembershipStatus.Active)
            .SelectMany(m => m.Roles.Where(a => a.RevokedAt == null))
            .Select(a => new { a.RoleCode, a.ScopeType, a.ScopeId })
            .ToListAsync(cancellationToken);
        return new HeldRoles([.. held
            .Select(a => (Role: roles.Find(a.RoleCode), a.ScopeType, a.ScopeId))
            .Where(a => a.Role is { Scope: RoleScope.Tenant })
            .Select(a => new HeldRole(a.Role!, a.ScopeType, a.ScopeId))]);
    }
}

/// <summary>A role the actor holds and where: tenant-wide (<c>TENANT</c>, no scope ID) or at one resource; platform roles have no scope.</summary>
public sealed record HeldRole(RoleDefinition Role, AssignmentScope ScopeType, Guid? ScopeId);

/// <summary>The roles one actor holds in one place (the platform, or one tenant), and what they allow (S03 D10).</summary>
public sealed class HeldRoles(IReadOnlyList<HeldRole> held)
{
    public static HeldRoles None { get; } = new([]);

    public IReadOnlyList<HeldRole> Held { get; } = held;

    /// <summary>
    /// Whether a held role grants the action: a platform role a platform action; a tenant role a tenant action when the
    /// assignment's scope type can grant it (<c>scopable_by</c>) and the assignment is tenant-wide or its scope is in
    /// <paramref name="chain"/>, the request's resource and the scopes above it (empty without a resource). Platform
    /// roles never grant tenant actions, and no role grants an account action.
    /// </summary>
    public bool Allows(ActionDefinition action, IReadOnlySet<ScopeRef> chain)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(chain);
        return Held.Any(held => held.Role.Grants(action) && (action.Scope, held.Role.Scope) switch
        {
            (ActionScope.Platform, RoleScope.Platform) => true,
            (ActionScope.Tenant, RoleScope.Tenant) => action.ScopableBy.Contains(held.ScopeType)
                && (held.ScopeType == AssignmentScope.Tenant
                    || (held.ScopeId is { } scopeId && chain.Contains(new ScopeRef(held.ScopeType, scopeId)))),
            _ => false,
        });
    }

    /// <summary>Whether only a resource's chain could still grant the action: some held role grants it at a scope other than the tenant.</summary>
    public bool GrantsBelowTheTenant(ActionDefinition action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Held.Any(held => held.ScopeType != AssignmentScope.Tenant && held.Role.Scope == RoleScope.Tenant
            && action.ScopableBy.Contains(held.ScopeType) && held.Role.Grants(action));
    }
}
