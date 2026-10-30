using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.GetTenantSubscription;

/// <summary>
/// The request's tenant's subscription, entitlements and usage (S04 D15), from the access inputs step 1 reads (cached,
/// D7), so the page agrees with what the tenant may do. Usage is counted on every read.
/// </summary>
public sealed class GetTenantSubscriptionHandler(
    TenantAccessReader reader,
    ITenantContext tenantContext,
    IEntitlementCatalogue entitlements,
    IPlanCatalogue plans,
    QuotaUsages usages)
{
    public async Task<TenantSubscriptionResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant actions run under the request's tenant.");
        var inputs = await reader.InputsAsync(tenantId, cancellationToken);
        var planValues = await reader.PlanValuesAsync(cancellationToken);
        var today = reader.TodayIn(inputs.TimeZone);
        var subscription = new TenantSubscription(inputs.Periods);
        var effective = EffectiveEntitlements.On(today, subscription, new TenantEntitlementOverrides(inputs.Overrides), planValues.Of);

        var current = subscription.CurrentOn(today);
        // With no period started, the current period is a scheduled one: it isn't the next as well (#72 R1).
        var next = subscription.NextScheduledOn(today) is { } scheduled && scheduled != current ? scheduled : null;
        return new TenantSubscriptionResponse(
            today,
            inputs.TimeZone,
            TenantSubscriptionPeriod.From(current, current is null ? null : plans.Find(current.PlanCode)?.Name, today),
            TenantSubscriptionPeriod.From(next, next is null ? null : plans.Find(next.PlanCode)?.Name, today),
            [.. entitlements.All.Select(definition => TenantEntitlementResponse.From(definition, effective.Of(definition)))],
            await usages.ReadAsync(entitlements.All, effective, cancellationToken));
    }
}
