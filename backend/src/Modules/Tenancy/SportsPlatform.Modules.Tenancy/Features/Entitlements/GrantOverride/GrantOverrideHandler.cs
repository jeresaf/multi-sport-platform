using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements.GrantOverride;

/// <summary>
/// <c>entitlement.override</c>: a new override, replacing an unremoved one of the same entitlement in the same save
/// (S04 D5). <see cref="CommonErrors.InvalidRequest"/> when <c>effectiveUntil</c> is before the tenant's today.
/// </summary>
public sealed class GrantOverrideHandler(
    SubscriptionChanges subscriptions,
    IEntitlementCatalogue entitlements,
    AuditScope audit,
    ICurrentActor actor,
    TimeProvider timeProvider)
{
    public Task<Result<SubscriptionResponse>> HandleAsync(Guid tenantId, GrantOverrideRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = entitlements.Find(request.EntitlementCode!)
            ?? throw new InvalidOperationException("The validator accepts declared entitlements only.");
        var reason = request.Reason!.Trim();
        audit.Describe(reason: reason);
        return subscriptions.ChangeOverridesAsync(
            tenantId,
            (overrides, today) =>
            {
                if (request.EffectiveUntil < today)
                {
                    return CommonErrors.InvalidRequest;
                }

                var granted = overrides.Grant(definition, request.Value!, reason, request.EffectiveUntil, actor, timeProvider.GetUtcNow());
                return granted.IsFailure
                    ? granted.Error
                    : Result.Success(new OverrideChange(granted.Value.Granted, new OverrideAuditMetadata(
                        definition.Code, granted.Value.Granted.Value, granted.Value.Granted.EffectiveUntil, granted.Value.Replaced?.Id)));
            },
            cancellationToken);
    }
}
