using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Identity.Features.Invitations.RevokeInvitation;

/// <summary>The optional body of <c>revoke</c>: <c>version</c>, when sent, must be the invitation's current one.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevokeInvitationRequest(uint? Version);
