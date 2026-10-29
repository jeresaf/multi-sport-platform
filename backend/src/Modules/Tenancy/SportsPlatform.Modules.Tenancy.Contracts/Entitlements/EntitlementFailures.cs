using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>
/// The failure <see cref="IEntitlements.CheckQuotaAsync"/> returns, for the modules that count quotas to recognise it and
/// declare it on their endpoints. Declared for the error catalogue as <c>TenancyErrors.QuotaExceeded</c>, the same
/// instance.
/// </summary>
public static class EntitlementFailures
{
    /// <summary>
    /// A create that would take the tenant past a quota of its plan or override (S04 D11, D12): retrying never helps, so
    /// it is 403, not 429.
    /// </summary>
    public static Error QuotaExceeded { get; } = new(
        "ENTITLEMENT_QUOTA_EXCEEDED", "The tenant has reached a limit of its plan.", ErrorKind.Forbidden);
}
