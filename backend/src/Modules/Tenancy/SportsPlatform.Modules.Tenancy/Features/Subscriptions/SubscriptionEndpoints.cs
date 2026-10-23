using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.AssignPeriod;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.CancelSubscription;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.ChangePlan;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.GetSubscription;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions.RenewSubscription;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions;

/// <summary>
/// <c>/api/platform/v1/tenants/{tenantId}/subscription</c> (S04 D16): the read and the four commands, each run under the
/// route's tenant. Every command answers with the whole subscription as it is after the change.
/// </summary>
internal static class SubscriptionEndpoints
{
    public const string Tag = "Subscriptions";

    private const string _datesNote =
        "Dates are the tenant's (its time zone in settings) and both `startsOn` and `endsOn` are included.";

    public static void Map(IEndpointRouteBuilder tenants)
    {
        var subscription = tenants.MapGroup("/{tenantId:guid}/subscription").WithTags(Tag);

        subscription.MapGet("/", (Guid tenantId, GetSubscriptionHandler handler, CancellationToken cancellationToken) =>
                OkOrProblemAsync(handler.HandleAsync(tenantId, cancellationToken)))
            .WithName("GetSubscription")
            .WithSummary("Get a tenant's subscription")
            .WithDescription(
                "Every subscription period of the tenant, latest start first, with its status on `today` (the tenant's " +
                "date), the current period (the one that decides access) and the next scheduled one. Statuses are " +
                "derived from the dates: SCHEDULED, ACTIVE, EXPIRING, GRACE, EXPIRED or CANCELLED.")
            .RequireAction(TenancyActions.SubscriptionRead)
            .RunsUnderRouteTenant()
            .ProducesErrors(TenancyErrors.NotFound);

        subscription.MapPost("/periods", async (
                Guid tenantId, AssignPeriodRequest request, AssignPeriodHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(tenantId, request, cancellationToken);
                return result.IsFailure
                    ? (Results<Created<SubscriptionResponse>, ProblemHttpResult>)result.Error.ToProblem()
                    : TypedResults.Created($"/api/platform/v1/tenants/{tenantId}/subscription", result.Value);
            })
            .WithName("AssignSubscriptionPeriod")
            .WithSummary("Give a tenant a subscription period")
            .WithDescription(
                $"A new period on `planCode` (an active plan) from `startsOn` to `endsOn`. {_datesNote} `kind` (PAID, " +
                "TRIAL or COMPLIMENTARY) is for reporting only. `expiringWindowDays` defaults to 30 and `graceDays` to 14 " +
                "(0–365). A period that shares a day with another period that isn't cancelled is refused " +
                "(TENANT_SUBSCRIPTION_OVERLAP). A trial, an event pass or the pilot offer is an ordinary period.")
            .RequireAction(TenancyActions.SubscriptionAssign)
            .RunsUnderRouteTenant()
            .ValidateRequest<AssignPeriodRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.PlanNotFound, TenancyErrors.SubscriptionOverlap);

        subscription.MapPost("/renew", async (
                Guid tenantId, RenewSubscriptionRequest request, RenewSubscriptionHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(tenantId, request, cancellationToken);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : RenewProblem(result.Error);
            })
            .WithName("RenewSubscription")
            .WithSummary("Renew a tenant's subscription")
            .WithDescription(
                "A new period from the day after the latest period (that isn't cancelled) ends, or from today if that " +
                $"day has passed or every period is cancelled, to `endsOn`. {_datesNote} `planCode`, `kind` and the " +
                "windows not sent are the latest period's: a lower plan is chosen here. Without any period, " +
                "TENANT_SUBSCRIPTION_NOT_FOUND (assign one instead); an `endsOn` before the first day, " +
                "VALIDATION_INVALID_REQUEST on `endsOn`; no `planCode` while the latest period's plan is no longer active, " +
                "TENANT_PLAN_NOT_FOUND on `planCode`.")
            .RequireAction(TenancyActions.SubscriptionRenew)
            .RunsUnderRouteTenant()
            .ValidateRequest<RenewSubscriptionRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                TenancyErrors.NotFound, TenancyErrors.PlanNotFound, TenancyErrors.SubscriptionNotFound, TenancyErrors.SubscriptionOverlap);

        subscription.MapPost("/change-plan", (
                Guid tenantId, ChangePlanRequest request, ChangePlanHandler handler, CancellationToken cancellationToken) =>
                OkOrProblemAsync(handler.HandleAsync(tenantId, request, cancellationToken)))
            .WithName("ChangeSubscriptionPlan")
            .WithSummary("Move a tenant to a higher plan now")
            .WithDescription(
                "Cancels the current period (reason \"Changed to <plan>\") and adds one on `planCode` from today to the " +
                "same `endsOn`, with the same kind and windows; the history keeps both. Only to a higher plan " +
                "(TENANT_SUBSCRIPTION_DOWNGRADE_AT_RENEWAL otherwise: choose a lower plan when renewing), and only while " +
                "the current period is ACTIVE or EXPIRING (TENANT_SUBSCRIPTION_NOT_FOUND otherwise: renew instead).")
            .RequireAction(TenancyActions.SubscriptionChangePlan)
            .RunsUnderRouteTenant()
            .ValidateRequest<ChangePlanRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(
                TenancyErrors.NotFound,
                TenancyErrors.PlanNotFound,
                TenancyErrors.SubscriptionNotFound,
                TenancyErrors.SubscriptionDowngradeAtRenewal,
                TenancyErrors.SubscriptionOverlap);

        subscription.MapPost("/cancel", (
                Guid tenantId, CancelSubscriptionRequest request, CancelSubscriptionHandler handler, CancellationToken cancellationToken) =>
                OkOrProblemAsync(handler.HandleAsync(tenantId, request, cancellationToken)))
            .WithName("CancelSubscription")
            .WithSummary("Cancel a tenant's subscription")
            .WithDescription(
                "Cancels the current period, unless it has expired, and every scheduled period, now, with `reason`. " +
                "Nothing to cancel is TENANT_SUBSCRIPTION_NOT_FOUND. The reason is kept permanently on the periods and " +
                "in the audit trail: don't include personal data.")
            .RequireAction(TenancyActions.SubscriptionCancel)
            .RunsUnderRouteTenant()
            .ValidateRequest<CancelSubscriptionRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.SubscriptionNotFound);
    }

    private static async Task<Results<Ok<SubscriptionResponse>, ProblemHttpResult>> OkOrProblemAsync(
        Task<Result<SubscriptionResponse>> handling)
    {
        var result = await handling;
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    // The renewal's first day and the plan it takes over are known only once the periods are read, so the handler
    // checks them; they are field errors like the validator's.
    private static Results<Ok<SubscriptionResponse>, ProblemHttpResult> RenewProblem(Error error)
    {
        if (error == CommonErrors.InvalidRequest)
        {
            return FieldProblem(new ValidationFailure(nameof(RenewSubscriptionRequest.EndsOn),
                "'endsOn' must be on or after the renewal's first day: the day after the latest period ends, or today."));
        }

        return error == TenancyErrors.PlanNotFound
            ? FieldProblem(new ValidationFailure(nameof(RenewSubscriptionRequest.PlanCode),
                "'planCode' must be sent: the latest period's plan is no longer active (GET /api/platform/v1/plans).")
            {
                ErrorCode = TenancyErrors.PlanNotFound.Code,
            })
            : error.ToProblem();
    }

    private static ProblemHttpResult FieldProblem(ValidationFailure failure) =>
        new ValidationResult([failure]).ToProblem(CommonErrors.InvalidRequest);
}
