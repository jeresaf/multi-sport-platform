using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.RenewSubscription;

/// <summary>
/// <c>subscription.renew</c>: a new period from the day after the latest one ends (today if that has passed). Without a
/// period, <c>TENANT_SUBSCRIPTION_NOT_FOUND</c>; an <c>endsOn</c> before the first day,
/// <see cref="CommonErrors.InvalidRequest"/>; a plan taken over from the latest period that is no longer active,
/// <c>TENANT_PLAN_NOT_FOUND</c> (the endpoint reports both on their field).
/// </summary>
public sealed class RenewSubscriptionHandler(SubscriptionChanges subscriptions)
{
    public async Task<Result<SubscriptionResponse>> HandleAsync(
        Guid tenantId, RenewSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (_, active) = await subscriptions.PlansAsync(cancellationToken);
        return await subscriptions.ChangeAsync(
            tenantId,
            (subscription, today) =>
            {
                var renewed = subscription.Renew(
                    request.EndsOn!.Value, request.PlanCode, request.Kind, request.ExpiringWindowDays, request.GraceDays, today);
                if (renewed.IsFailure)
                {
                    return renewed.Error;
                }

                return active.Contains(renewed.Value.PlanCode)
                    ? Result.Success(new SubscriptionChange(renewed.Value, SubscriptionAuditMetadata.Added(renewed.Value)))
                    : TenancyErrors.PlanNotFound;
            },
            cancellationToken);
    }
}
