using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Features.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Sports.ListTenantSports;

/// <summary>
/// <c>tenant.sport.read</c> (S05 D15): the published variants, in <see cref="ISportDefinitions"/>' order, with the
/// tenant's enablements. The quota's usage is added when the actor may also read the subscription: the same step 3 the
/// pipeline runs for <c>tenant.subscription.read</c>. Counted on every read.
/// </summary>
public sealed class ListTenantSportsHandler(
    ISportDefinitions definitions,
    EnabledSports enabledSports,
    IEntitlements entitlements,
    IAuthorizationEvaluator authorization,
    ICurrentActor actor,
    ITenantContext tenantContext)
{
    public async Task<TenantSportsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant actions run under the request's tenant.");
        var variants = await definitions.ListAsync(cancellationToken);
        var enabled = (await enabledSports.ListAsync(cancellationToken)).ToDictionary(s => s.Variant, s => s.EnabledAt, StringComparer.Ordinal);
        var rows = variants
            .Select(v => new TenantSportResponse(
                v.Variant, v.Sport, v.Name, v.ScoringFamily, enabled.ContainsKey(v.Variant), enabled.TryGetValue(v.Variant, out var since) ? since : null))
            .ToList();

        var mayReadSubscription = await authorization.AuthorizeAsync(
            new AuthorizationRequest(TenancyActions.TenantSubscriptionRead, actor, tenantId), cancellationToken);
        if (mayReadSubscription.IsFailure)
        {
            return new TenantSportsResponse(rows, SportQuota: null);
        }

        var used = await enabledSports.CountAsync(cancellationToken);
        var limit = await entitlements.GetLimitAsync(EnabledSports.Code, cancellationToken);
        return new TenantSportsResponse(
            rows, new QuotaUsageResponse(EnabledSports.Code, Measured: true, used, limit, EffectiveEntitlements.IsNearLimit(limit, used)));
    }
}
