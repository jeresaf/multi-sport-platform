using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements.RemoveOverride;

/// <summary>
/// <c>entitlement.override.remove</c>: removes an unremoved override now, with the reason; the plan's value applies again
/// (S04 D5). <c>TENANT_OVERRIDE_NOT_FOUND</c> for an override the tenant doesn't have or that is already removed.
/// </summary>
public sealed class RemoveOverrideHandler(SubscriptionChanges subscriptions, AuditScope audit, ICurrentActor actor, TimeProvider timeProvider)
{
    public Task<Result<SubscriptionResponse>> HandleAsync(
        Guid tenantId, Guid overrideId, RemoveOverrideRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        audit.Describe(SubscriptionChanges.OverrideAuditEntityType, overrideId, reason);
        return subscriptions.ChangeOverridesAsync(
            tenantId,
            (overrides, _) =>
            {
                var removed = overrides.Remove(overrideId, reason, actor, timeProvider.GetUtcNow());
                return removed.IsFailure
                    ? removed.Error
                    : Result.Success(new OverrideChange(removed.Value, new OverrideAuditMetadata(removed.Value.EntitlementCode, null, null, null)));
            },
            cancellationToken);
    }
}
