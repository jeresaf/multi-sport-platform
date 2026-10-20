using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Identity.Features.Members.RevokeRole;

/// <summary>The optional body of <c>revoke</c>: <c>version</c>, when sent, must be the membership's current one.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeRoleRequest(uint? Version = null);
