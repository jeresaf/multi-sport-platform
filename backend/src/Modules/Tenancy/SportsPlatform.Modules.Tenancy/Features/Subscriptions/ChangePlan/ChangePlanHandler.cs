using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.ChangePlan;

/// <summary>
/// <c>subscription.change_plan</c>: now, to a higher-ranked plan, until the current period ends. The current period is
/// cancelled ("Changed to &lt;plan&gt;") and a new one added in the same save, so the history keeps both.
/// </summary>
public sealed class ChangePlanHandler(SubscriptionChanges subscriptions, ICurrentActor actor, TimeProvider timeProvider)
{
    public async Task<Result<SubscriptionResponse>> HandleAsync(Guid tenantId, ChangePlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var planCode = request.PlanCode!;
        // The validator has checked that the plan is active (a field error, #65 R4).
        var (ranks, _) = await subscriptions.PlansAsync(cancellationToken);
        return await subscriptions.ChangeAsync(
            tenantId,
            (subscription, today) =>
            {
                var current = subscription.CurrentOn(today);
                var changed = subscription.ChangePlan(
                    planCode, code => ranks.TryGetValue(code, out var rank) ? rank : null, actor, timeProvider.GetUtcNow(), today);
                return changed.IsFailure
                    ? changed.Error
                    : Result.Success(new SubscriptionChange(changed.Value, SubscriptionAuditMetadata.Added(changed.Value, current!.Id)));
            },
            cancellationToken);
    }
}
