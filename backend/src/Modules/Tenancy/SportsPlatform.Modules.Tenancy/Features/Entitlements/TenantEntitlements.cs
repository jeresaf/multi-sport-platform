using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Access;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements;

/// <summary>
/// <see cref="IEntitlements"/> for the scope's tenant (S04 D6): its current period's plan and the overrides in effect
/// on the tenant's date, from <see cref="TenantAccessReader"/> (cached inputs, D7).
/// </summary>
internal sealed class TenantEntitlements(TenantAccessReader reader, ITenantContext tenantContext, IEntitlementCatalogue catalogue)
    : IEntitlements
{
    public async Task<EffectiveEntitlement> GetAsync(string code, CancellationToken cancellationToken)
    {
        var definition = Definition(code);
        return (await EffectiveAsync(cancellationToken)).Of(definition);
    }

    public async Task<bool> CanUseAsync(string code, CancellationToken cancellationToken) =>
        EffectiveEntitlements.Allows((await GetAsync(code, cancellationToken)).Value);

    public async Task<IReadOnlyList<string>> PlansAllowingAsync(string code, CancellationToken cancellationToken)
    {
        var definition = Definition(code);
        if (definition.Type == EntitlementType.Quota)
        {
            throw new ArgumentException($"Entitlement {code} is a quota: it is counted, not allowed.", nameof(code));
        }

        return (await reader.PlanValuesAsync(cancellationToken)).PlansAllowing(code);
    }

    public async Task<int?> GetLimitAsync(string code, CancellationToken cancellationToken)
    {
        var value = (await GetAsync(code, cancellationToken)).Value;
        return value.Type == EntitlementType.Quota
            ? value.Limit
            : throw new ArgumentException($"Entitlement {code} is not a quota.", nameof(code));
    }

    public async Task<Result<QuotaCheck>> CheckQuotaAsync(string code, int used, CancellationToken cancellationToken) =>
        EffectiveEntitlements.CheckQuota((await GetAsync(code, cancellationToken)).Value, used);

    private EntitlementDefinition Definition(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return catalogue.Find(code) ?? throw new ArgumentException($"No entitlement {code} is declared.", nameof(code));
    }

    private async Task<EffectiveEntitlements> EffectiveAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("Set the scope's tenant before reading its entitlements.");
        var inputs = await reader.InputsAsync(tenantId, cancellationToken);
        var planValues = await reader.PlanValuesAsync(cancellationToken);
        return EffectiveEntitlements.On(
            reader.TodayIn(inputs.TimeZone), new TenantSubscription(inputs.Periods), new TenantEntitlementOverrides(inputs.Overrides), planValues.Of);
    }
}
