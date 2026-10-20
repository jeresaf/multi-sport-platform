using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Identity.Features.Members.AssignRole;

/// <summary>
/// A role to give a member: its code and scope (<c>TENANT</c> when not sent; the only scope S03 accepts, so
/// <c>scopeId</c> stays empty). <c>version</c>, when sent, must be the membership's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignRoleRequest(
    string? RoleCode, AssignmentScope? ScopeType = null, Guid? ScopeId = null, uint? Version = null);
