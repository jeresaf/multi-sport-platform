using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;

/// <summary>
/// The optional body of <c>activate</c> and <c>restore</c>: <c>version</c>, when sent, must be the tenant's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeTenantStatusRequest(uint? Version = null);

/// <summary>
/// The body of <c>suspend</c>, <c>archive</c> and <c>unarchive</c> (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept
/// permanently in the status history and the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeTenantStatusWithReasonRequest(string? Reason, uint? Version = null);
