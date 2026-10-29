using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.GetTenantSubscription;

/// <summary><c>GET /api/admin/v1/tenant/subscription</c>: the request's tenant's plan, entitlements and usage (S04 D15).</summary>
internal static class GetTenantSubscriptionEndpoint
{
    public static void Map(IEndpointRouteBuilder admin) => admin
        .MapGet("/tenant/subscription", HandleAsync)
        .WithTags(SubscriptionEndpoints.Tag)
        .WithName("GetTenantSubscription")
        .WithSummary("Get the tenant's subscription")
        .WithDescription(
            "The subscription of the tenant named by `X-Tenant-Id`: the current period (plan, kind, status today and days, " +
            "all in the tenant's time zone; `null` without a period) and the next scheduled one; every entitlement's value " +
            "today with its source (DEFAULT, PLAN or OVERRIDE); and every quota's usage (`used` of `limit`, `null` = " +
            "unlimited; `warning` from 80 % of the limit; `measured: false` while no module counts it yet). Available while " +
            "the tenant is read-only. To change the plan, the tenant contacts the platform.")
        .RequireAction(TenancyActions.TenantSubscriptionRead);

    private static async Task<Ok<TenantSubscriptionResponse>> HandleAsync(
        GetTenantSubscriptionHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
