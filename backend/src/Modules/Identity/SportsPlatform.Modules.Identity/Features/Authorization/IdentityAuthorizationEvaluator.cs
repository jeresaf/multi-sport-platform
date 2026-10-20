using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure;

namespace SportsPlatform.Modules.Identity.Features.Authorization;

/// <summary>
/// Step 3 of the decision formula, scoped permission (S03 D10), read per request (no claims cache, D5):
/// <list type="bullet">
/// <item>an account action is the caller's own account: allowed to a signed-in user, and to an anonymous caller when
/// the action allows one (the pipeline answers the others 401 first);</item>
/// <item>a platform action needs an unrevoked platform role that grants it;</item>
/// <item>a tenant action needs an active membership in the request's tenant and an unrevoked assignment whose role
/// grants the action, whose scope type can grant it (the action's <c>scopable_by</c>), and whose scope is the tenant
/// or, for a request on a resource, in that resource's scope chain. Read under the request's tenant (RLS). Platform
/// roles never grant tenant actions.</item>
/// </list>
/// </summary>
public sealed class IdentityAuthorizationEvaluator(
    IdentityDbContext context, RoleCatalogue roles, ITenantContext tenantContext, IEnumerable<IScopeChainProvider> scopeChains)
    : IAuthorizationEvaluator
{
    public async ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = request.Action.Scope switch
        {
            ActionScope.Account => request.Actor.Kind != ActorKind.Anonymous || request.Action.AllowsAnonymous,
            ActionScope.Platform => await HasPlatformRoleGrantingAsync(request, cancellationToken),
            ActionScope.Tenant => await HasTenantRoleGrantingAsync(request, cancellationToken),
            _ => false,
        };
        return allowed ? Result.Success() : Result.Failure(AuthorizationErrors.Forbidden);
    }

    private async Task<bool> HasPlatformRoleGrantingAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        if (request.Actor.Kind != ActorKind.User)
        {
            return false;
        }

        var held = await context.Set<PlatformRoleAssignment>().AsNoTracking()
            .Where(a => a.UserId == request.Actor.ActorId && a.RevokedAt == null)
            .Select(a => a.RoleCode)
            .ToListAsync(cancellationToken);
        return held.Any(code => roles.Find(code) is { Scope: RoleScope.Platform } role && role.Grants(request.Action));
    }

    private async Task<bool> HasTenantRoleGrantingAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        // The roles are read under RLS, so the request's tenant must be the scope's (tenant resolution sets both).
        if (request.Actor.Kind != ActorKind.User || request.TenantId is not { } tenantId || tenantContext.TenantId != tenantId)
        {
            return false;
        }

        var held = await context.Set<TenantMembership>().AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UserId == request.Actor.ActorId && m.Status == MembershipStatus.Active)
            .SelectMany(m => m.Roles.Where(a => a.RevokedAt == null))
            .Select(a => new { a.RoleCode, a.ScopeType, a.ScopeId })
            .ToListAsync(cancellationToken);

        var granting = held
            .Where(a => request.Action.ScopableBy.Contains(a.ScopeType)
                && roles.Find(a.RoleCode) is { Scope: RoleScope.Tenant } role
                && role.Grants(request.Action))
            .ToList();
        if (granting.Any(a => a.ScopeType == AssignmentScope.Tenant))
        {
            return true;
        }

        if (request.Scope is not { } resource || granting.Count == 0)
        {
            return false;
        }

        var chain = await ChainAsync(resource, cancellationToken);
        return granting.Any(a => a.ScopeId is { } scopeId && chain.Contains(new ScopeRef(a.ScopeType, scopeId)));
    }

    /// <summary>The resource and the scopes above it; just the resource when no module provides its chain.</summary>
    private async Task<HashSet<ScopeRef>> ChainAsync(ScopeRef resource, CancellationToken cancellationToken)
    {
        var chain = new HashSet<ScopeRef> { resource };
        if (scopeChains.SingleOrDefault(provider => provider.ResourceType == resource.Type) is { } provider)
        {
            chain.UnionWith(await provider.GetAncestorsAsync(resource.Id, cancellationToken));
        }

        return chain;
    }
}
