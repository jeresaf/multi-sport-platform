using SportsPlatform.BuildingBlocks.Entitlements;
using static SportsPlatform.BuildingBlocks.Entitlements.EntitlementValue;

namespace SportsPlatform.Modules.Tenancy.Domain.Plans;

/// <summary>
/// The plans and their values: docs/architecture/authorization.md's table ("Entitlements and plan values"), which stays
/// the source of truth (a test compares the two). Prices and descriptions: docs/product/pricing-and-plans.md.
/// </summary>
public static class TenancyPlans
{
    public static readonly PlanDefinition Standard = new("STANDARD", "Standard", rank: 1, Values(
        (TenancyEntitlements.MaxEnabledSports, Quota(3)),
        (TenancyEntitlements.MaxActiveEditions, Quota(8)),
        (TenancyEntitlements.MaxTeams, Quota(120)),
        (TenancyEntitlements.MaxStaffUsers, Quota(100)),
        (TenancyEntitlements.StorageGb, Quota(10)),
        (TenancyEntitlements.LiveScoring, Flag(true)),
        (TenancyEntitlements.OfflineScoring, Flag(true)),
        (TenancyEntitlements.PublicSite, Flag(true)),
        (TenancyEntitlements.HistoricalResults, Flag(true)),
        (TenancyEntitlements.Branding, Level("BASIC")),
        (TenancyEntitlements.CustomDomain, Flag(false)),
        (TenancyEntitlements.Sponsorship, Level("BASIC")),
        (TenancyEntitlements.BulkImport, Level("REGISTRY")),
        (TenancyEntitlements.Reports, Level("BASIC")),
        (TenancyEntitlements.Transfers, Flag(false)),
        (TenancyEntitlements.PublicNotifications, Flag(false)),
        (TenancyEntitlements.ApiAccess, Flag(false))));

    public static readonly PlanDefinition Pro = new("PRO", "Pro", rank: 2, Values(
        (TenancyEntitlements.MaxEnabledSports, UnlimitedQuota),
        (TenancyEntitlements.MaxActiveEditions, Quota(60)),
        (TenancyEntitlements.MaxTeams, Quota(2_000)),
        (TenancyEntitlements.MaxStaffUsers, Quota(1_000)),
        (TenancyEntitlements.StorageGb, Quota(100)),
        (TenancyEntitlements.LiveScoring, Flag(true)),
        (TenancyEntitlements.OfflineScoring, Flag(true)),
        (TenancyEntitlements.PublicSite, Flag(true)),
        (TenancyEntitlements.HistoricalResults, Flag(true)),
        (TenancyEntitlements.Branding, Level("FULL")),
        (TenancyEntitlements.CustomDomain, Flag(true)),
        (TenancyEntitlements.Sponsorship, Level("FULL")),
        (TenancyEntitlements.BulkImport, Level("FULL")),
        (TenancyEntitlements.Reports, Level("ADVANCED")),
        (TenancyEntitlements.Transfers, Flag(true)),
        (TenancyEntitlements.PublicNotifications, Flag(true)),
        (TenancyEntitlements.ApiAccess, Flag(true))));

    /// <summary>In rank order. Code reads them through <see cref="IPlanCatalogue"/>, which tests can replace.</summary>
    public static IReadOnlyList<PlanDefinition> All { get; } = [Standard, Pro];

    private static Dictionary<string, EntitlementValue> Values(params (EntitlementDefinition Entitlement, EntitlementValue Value)[] values) =>
        values.ToDictionary(entry => entry.Entitlement.Code, entry => entry.Value, StringComparer.Ordinal);
}
