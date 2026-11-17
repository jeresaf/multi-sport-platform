using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;

/// <summary>
/// The optional body of <c>deactivate</c> and <c>reactivate</c>: <c>version</c>, when sent, must be the person's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangePersonStatusRequest(uint? Version = null);

/// <summary>
/// The body of <c>archive</c>, <c>unarchive</c> and <c>mark-deceased</c> (audit <c>REQUIRED_WITH_REASON</c>). The reason
/// is kept permanently in the status history and the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangePersonStatusWithReasonRequest(string? Reason, uint? Version = null);
