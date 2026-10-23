using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.AssignPeriod;

/// <summary><c>subscription.assign</c>: a new period on the dates given; <c>TENANT_SUBSCRIPTION_OVERLAP</c> if it overlaps.</summary>
public sealed class AssignPeriodHandler(SubscriptionChanges subscriptions)
{
    public Task<Result<SubscriptionResponse>> HandleAsync(Guid tenantId, AssignPeriodRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var terms = new SubscriptionTerms(
            request.PlanCode!,
            request.Kind!.Value,
            request.StartsOn!.Value,
            request.EndsOn!.Value,
            request.ExpiringWindowDays ?? SubscriptionPeriod.DefaultExpiringWindowDays,
            request.GraceDays ?? SubscriptionPeriod.DefaultGraceDays);
        return subscriptions.ChangeAsync(tenantId, (subscription, _) => Audited(subscription.Assign(terms)), cancellationToken);
    }

    private static Result<SubscriptionChange> Audited(Result<SubscriptionPeriod> added) => added.IsFailure
        ? added.Error
        : Result.Success(new SubscriptionChange(added.Value, SubscriptionAuditMetadata.Added(added.Value)));
}
