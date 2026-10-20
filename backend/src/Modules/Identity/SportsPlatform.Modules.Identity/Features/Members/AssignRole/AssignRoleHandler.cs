using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.Roles;

namespace SportsPlatform.Modules.Identity.Features.Members.AssignRole;

/// <summary>
/// Gives a member a tenant role. S03 assigns tenant-wide only (owner, S03-T4b): no module can yet confirm that a
/// competition, team or contest exists, so any other scope, like an unknown role or a platform role, is
/// <c>AUTH_ROLE_NOT_ASSIGNABLE</c>. Only an owner grants <c>TENANT_OWNER</c>. Takes effect on the member's next request.
/// </summary>
public sealed class AssignRoleHandler(MemberChanges changes, RoleCatalogue roles, ICurrentActor actor, TimeProvider timeProvider)
{
    public async Task<Result<MemberResponse>> HandleAsync(
        Guid membershipId, AssignRoleRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeType = request.ScopeType ?? AssignmentScope.Tenant;
        if (roles.Find(request.RoleCode!) is not { } role || scopeType != AssignmentScope.Tenant)
        {
            return IdentityErrors.RoleNotAssignable;
        }

        return await changes.ApplyAsync(
            membershipId,
            reason: null,
            request.Version,
            ifMatch,
            _ => role.Code == IdentityRoles.TenantOwner ? OwnerImpact.ChangesOwner : OwnerImpact.None,
            membership => membership.AssignRole(role, scopeType, request.ScopeId, actor, timeProvider.GetUtcNow()),
            cancellationToken);
    }
}
