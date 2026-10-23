using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Features.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.GetSubscription;

/// <summary>A tenant's subscription periods, read under the route's tenant; the read is audited with it (S04 D16).</summary>
public sealed class GetSubscriptionHandler(SubscriptionChanges subscriptions, AuditScope audit)
{
    public Task<Result<SubscriptionResponse>> HandleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        audit.Describe(TenantAudit.EntityType, tenantId);
        return subscriptions.ReadAsync(tenantId, cancellationToken);
    }
}
