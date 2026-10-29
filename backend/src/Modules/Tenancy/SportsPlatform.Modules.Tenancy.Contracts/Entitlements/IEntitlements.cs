using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

/// <summary>
/// The effective entitlements of the scope's tenant (S04 D6): the entitlement's default, then the current subscription
/// period's plan value, then an unremoved override in effect today. Call after the scope's tenant is set (the periods
/// and overrides are read under RLS). The tenant's inputs are cached for up to a minute and cleared when they change
/// (D7); "today" is computed on every call. An unknown entitlement code is a bug and throws.
/// </summary>
public interface IEntitlements
{
    Task<EffectiveEntitlement> GetAsync(string code, CancellationToken cancellationToken);

    /// <summary>A boolean that is on, or a level entitlement at any level (the caller checks the level it needs).</summary>
    Task<bool> CanUseAsync(string code, CancellationToken cancellationToken);

    /// <summary>A quota's limit; null is unlimited.</summary>
    Task<int?> GetLimitAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Whether one more item fits the quota, given <paramref name="used"/>, the items the tenant has before this create
    /// (counted by the module that owns them, under its per-tenant lock, D12): <c>ENTITLEMENT_QUOTA_EXCEEDED</c> when
    /// <paramref name="used"/> has reached the limit; otherwise the check, with <see cref="QuotaCheck.Warning"/> when the
    /// new item takes the tenant to 80 % of the limit or more.
    /// </summary>
    Task<Result<QuotaCheck>> CheckQuotaAsync(string code, int used, CancellationToken cancellationToken);
}
