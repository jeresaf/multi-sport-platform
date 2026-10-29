using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>Where a tenant's effective value of an entitlement comes from (S04 D6).</summary>
public enum EntitlementSource
{
    /// <summary>The definition's default (off, 0, no level): no period, or its plan has no value for the entitlement.</summary>
    Default,

    /// <summary>The current subscription period's plan.</summary>
    Plan,

    /// <summary>An unremoved override in effect today.</summary>
    Override,
}

/// <summary>
/// A tenant's value of one entitlement and where it comes from. <c>PlanCode</c> is the current period's plan (also when
/// an override or the default decides); null without a period.
/// </summary>
public sealed record EffectiveEntitlement(string Code, EntitlementValue Value, EntitlementSource Source, string? PlanCode);

/// <summary>
/// A quota that has room for one more item: its limit (null = unlimited), the items used before this create, and whether
/// the new item takes the tenant to 80 % of the limit or more (shown as a warning).
/// </summary>
public sealed record QuotaCheck(int? Limit, int Used, bool Warning);
