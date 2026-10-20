using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Identity.Features.Members.ChangeMemberStatus;

/// <summary>The optional body of <c>restore</c>: <c>version</c>, when sent, must be the membership's current one.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeMemberStatusRequest(uint? Version = null);

/// <summary>
/// The body of <c>suspend</c> and <c>remove</c> (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on
/// the membership and in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeMemberStatusWithReasonRequest(string? Reason, uint? Version = null);
