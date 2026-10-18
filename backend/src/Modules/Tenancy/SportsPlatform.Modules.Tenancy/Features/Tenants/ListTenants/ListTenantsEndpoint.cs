using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

internal static class ListTenantsEndpoint
{
    public static void Map(IEndpointRouteBuilder tenants) => tenants
        .MapGet("/", HandleAsync)
        .WithName("ListTenants")
        .WithSummary("List tenants")
        .WithDescription(
            "Every tenant, ordered by slug. Filter by `status` (PROVISIONING, ACTIVE, SUSPENDED or ARCHIVED); `limit` is " +
            "1–100 (25). Every page read is audited.")
        .RequireAction(TenancyActions.TenantRead)
        .ValidateRequest<ListTenantsRequest>(CommonErrors.InvalidQuery);

    private static async Task<Ok<ListTenantsResponse>> HandleAsync(
        [AsParameters] ListTenantsRequest request,
        ListTenantsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));
}
