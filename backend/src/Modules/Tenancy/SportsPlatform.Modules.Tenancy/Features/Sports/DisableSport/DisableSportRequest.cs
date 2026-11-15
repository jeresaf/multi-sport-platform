using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Sports.DisableSport;

/// <summary>
/// The body of <c>tenant.sport.disable</c> (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the
/// enablement and in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DisableSportRequest(string? Reason);
