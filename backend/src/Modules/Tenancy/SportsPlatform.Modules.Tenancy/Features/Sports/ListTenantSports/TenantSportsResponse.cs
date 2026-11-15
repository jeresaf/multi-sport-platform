using SportsPlatform.Modules.Tenancy.Features.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Sports.ListTenantSports;

/// <summary>
/// Every published sport variant and whether the tenant has it enabled (S05 D15). <c>sportQuota</c> is the
/// <c>MAX_ENABLED_SPORTS</c> usage for those who may read the subscription (<c>tenant.subscription.read</c>), else null.
/// </summary>
public sealed record TenantSportsResponse(IReadOnlyList<TenantSportResponse> Variants, QuotaUsageResponse? SportQuota);

/// <summary>
/// A published variant at its latest version: <c>enabledSince</c> is when its current enablement began (null when it
/// isn't enabled).
/// </summary>
public sealed record TenantSportResponse(
    string Variant, string Sport, string Name, string ScoringFamily, bool Enabled, DateTimeOffset? EnabledSince);
