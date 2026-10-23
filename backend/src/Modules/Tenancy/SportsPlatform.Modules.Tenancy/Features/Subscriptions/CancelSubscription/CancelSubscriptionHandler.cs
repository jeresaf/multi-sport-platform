using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.CancelSubscription;

/// <summary>
/// <c>subscription.cancel</c>: the current period (unless expired) and every scheduled one, now, with the reason. The
/// audit entry names the first period cancelled and lists them all.
/// </summary>
public sealed class CancelSubscriptionHandler(
    SubscriptionChanges subscriptions, AuditScope audit, ICurrentActor actor, TimeProvider timeProvider)
{
    public Task<Result<SubscriptionResponse>> HandleAsync(
        Guid tenantId, CancelSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        audit.Describe(reason: reason);
        return subscriptions.ChangeAsync(
            tenantId,
            (subscription, today) =>
            {
                var cancelled = subscription.Cancel(reason, actor, timeProvider.GetUtcNow(), today);
                return cancelled.IsFailure
                    ? cancelled.Error
                    : Result.Success(new SubscriptionChange(
                        cancelled.Value[0], new SubscriptionAuditMetadata(null, null, null, [.. cancelled.Value.Select(p => p.Id)])));
            },
            cancellationToken);
    }
}
