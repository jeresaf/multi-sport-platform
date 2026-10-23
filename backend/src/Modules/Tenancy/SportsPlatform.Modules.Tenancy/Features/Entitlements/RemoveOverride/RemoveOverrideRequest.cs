using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements.RemoveOverride;

/// <summary>
/// The body of <c>entitlement.override.remove</c> (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on
/// the override and in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveOverrideRequest(string? Reason);
