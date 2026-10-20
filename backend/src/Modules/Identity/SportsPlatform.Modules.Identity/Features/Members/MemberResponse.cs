using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.Members;

/// <summary>
/// A member of the tenant as the admin API shows them, with their active roles. The email is the user's only
/// identifier until people have profiles; shown to every holder of <c>membership.read</c> (owner, S03-T4b) and never
/// logged or audited. <c>version</c> (also the <c>ETag</c>) is the membership's; role changes change it too.
/// </summary>
public sealed record MemberResponse(
    Guid MembershipId,
    Guid UserId,
    [property: PersonalData(PersonalDataClass.Private)] string Email,
    MembershipStatus Status,
    DateTimeOffset JoinedAt,
    Guid? InvitedBy,
    string? StatusReason,
    DateTimeOffset? StatusChangedAt,
    IReadOnlyList<MemberRoleResponse> Roles,
    uint Version)
{
    /// <summary>The members among <paramref name="memberships"/>, with their users' emails (users are global, same module).</summary>
    public static IQueryable<MemberResponse> Project(IQueryable<TenantMembership> memberships, IQueryable<User> users) =>
        memberships.Join(users, m => m.UserId, u => u.Id, (m, u) => new MemberResponse(
            m.Id,
            m.UserId,
            u.Email!,
            m.Status,
            m.JoinedAt,
            m.InvitedBy,
            m.StatusReason,
            m.StatusChangedAt,
            m.Roles.Where(a => a.RevokedAt == null)
                .OrderBy(a => a.GrantedAt).ThenBy(a => a.Id)
                .Select(a => new MemberRoleResponse(a.Id, a.RoleCode, a.ScopeType, a.ScopeId, a.GrantedAt))
                .ToList(),
            EF.Property<uint>(m, AuditColumns.Version)));

    /// <summary>A tracked membership with its roles, after its save (the save reads the version back).</summary>
    public static MemberResponse From(TenantMembership membership, uint version, string email)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return new MemberResponse(
            membership.Id, membership.UserId, email, membership.Status, membership.JoinedAt, membership.InvitedBy,
            membership.StatusReason, membership.StatusChangedAt,
            [.. membership.ActiveRoles
                .OrderBy(a => a.GrantedAt).ThenBy(a => a.Id)
                .Select(a => new MemberRoleResponse(a.Id, a.RoleCode, a.ScopeType, a.ScopeId, a.GrantedAt))],
            version);
    }

    public void SetETag(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = RowVersion.EntityTag(Version).ToString();
    }
}

/// <summary>An active role assignment: the role, where it applies (<c>scopeId</c> null tenant-wide) and when it was granted.</summary>
public sealed record MemberRoleResponse(Guid AssignmentId, string RoleCode, AssignmentScope ScopeType, Guid? ScopeId, DateTimeOffset GrantedAt);
