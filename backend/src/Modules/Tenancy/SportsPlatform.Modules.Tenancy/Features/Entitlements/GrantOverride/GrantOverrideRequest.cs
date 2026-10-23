using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements.GrantOverride;

/// <summary>
/// The body of <c>entitlement.override</c> (audit <c>REQUIRED_WITH_REASON</c>): <c>value</c> in place of the plan's for
/// <c>entitlementCode</c>, until <c>effectiveUntil</c> (the tenant's date, included; none = until removed). The reason
/// is kept permanently in the override and the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GrantOverrideRequest(
    string? EntitlementCode,
    EntitlementValue? Value,
    string? Reason,
    DateOnly? EffectiveUntil = null);
