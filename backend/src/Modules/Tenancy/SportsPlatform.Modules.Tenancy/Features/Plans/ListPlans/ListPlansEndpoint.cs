using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Tenancy.Features.Plans.ListPlans;

internal static class ListPlansEndpoint
{
    public static void Map(IEndpointRouteBuilder plans) => plans
        .MapGet("/", HandleAsync)
        .WithName("ListPlans")
        .WithSummary("List plans")
        .WithDescription(
            "The active plans in rank order (STANDARD, then PRO), each with every entitlement's definition and value, " +
            "ordered by entitlement code. A value is `{ enabled }`, `{ limit }` (null = unlimited) or `{ level }` " +
            "(null = none). Plans change only through a reviewed seed (no editing). Every read is audited.")
        .RequireAction(TenancyActions.PlanRead);

    private static async Task<Ok<ListPlansResponse>> HandleAsync(ListPlansHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
