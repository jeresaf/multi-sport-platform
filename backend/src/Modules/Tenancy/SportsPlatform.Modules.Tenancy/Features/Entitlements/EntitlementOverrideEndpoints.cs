using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Features.Entitlements.GrantOverride;
using SportsPlatform.Modules.Tenancy.Features.Entitlements.RemoveOverride;
using SportsPlatform.Modules.Tenancy.Features.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Entitlements;

/// <summary>
/// <c>/api/platform/v1/tenants/{tenantId}/entitlement-overrides</c> (S04 D5, D16): grant and remove, each run under the
/// route's tenant. Both answer with the whole subscription as it is after the change, overrides included.
/// </summary>
internal static class EntitlementOverrideEndpoints
{
    private const string _reasonNote =
        "The reason is kept permanently on the override and in the audit trail, and tenants never see it: don't include personal data.";

    public static void Map(IEndpointRouteBuilder tenants)
    {
        var overrides = tenants.MapGroup("/{tenantId:guid}/entitlement-overrides").WithTags(SubscriptionEndpoints.Tag);

        overrides.MapPost("/", async (
                Guid tenantId, GrantOverrideRequest request, GrantOverrideHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(tenantId, request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/platform/v1/tenants/{tenantId}/subscription", result.Value)
                    : GrantProblem(result.Error);
            })
            .WithName("GrantEntitlementOverride")
            .WithSummary("Give a tenant its own value of an entitlement")
            .WithDescription(
                "`value` in place of the plan's for `entitlementCode` (a declared entitlement, GET /api/platform/v1/plans), " +
                "of its type: `{\"enabled\": true}`, `{\"limit\": 5}` (`null` = unlimited) or `{\"level\": \"FULL\"}` " +
                "(`null` = none); otherwise TENANT_OVERRIDE_INVALID_VALUE on the field. It applies until `effectiveUntil` " +
                "(the tenant's date, included; on or after its today, else VALIDATION_INVALID_REQUEST on the field) or, " +
                "without one, until it is removed. An override already in place for the entitlement is removed " +
                $"(\"{Domain.Entitlements.TenantEntitlementOverrides.ReplacedReason}\") in the same change; the history keeps both. {_reasonNote}")
            .RequireAction(TenancyActions.EntitlementOverride)
            .RunsUnderRouteTenant()
            .ValidateRequest<GrantOverrideRequest>(CommonErrors.InvalidRequest)
            .ProducesErrors(TenancyErrors.NotFound);

        overrides.MapPost("/{overrideId:guid}/remove", async (
                Guid tenantId,
                Guid overrideId,
                RemoveOverrideRequest request,
                RemoveOverrideHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(tenantId, overrideId, request, cancellationToken);
                return result.IsSuccess
                    ? (Results<Ok<SubscriptionResponse>, ProblemHttpResult>)TypedResults.Ok(result.Value)
                    : result.Error.ToProblem();
            })
            .WithName("RemoveEntitlementOverride")
            .WithSummary("Remove a tenant's entitlement override")
            .WithDescription(
                "Removes the override now, with `reason`: the plan's value applies again. An override the tenant doesn't " +
                $"have, or one already removed (or replaced), is TENANT_OVERRIDE_NOT_FOUND. {_reasonNote}")
            .RequireAction(TenancyActions.EntitlementOverrideRemove)
            .RunsUnderRouteTenant()
            .ValidateRequest<RemoveOverrideRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.OverrideNotFound);
    }

    // Whether effectiveUntil has passed is known only once the tenant's date is read, so the handler checks it; it is a
    // field error like the validator's.
    private static Results<Created<SubscriptionResponse>, ProblemHttpResult> GrantProblem(Error error) =>
        error == CommonErrors.InvalidRequest
            ? new ValidationResult([new ValidationFailure(nameof(GrantOverrideRequest.EffectiveUntil),
                "'effectiveUntil' must be on or after the tenant's today.")]).ToProblem(CommonErrors.InvalidRequest)
            : error.ToProblem();
}
