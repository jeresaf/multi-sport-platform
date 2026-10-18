using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.GetTenant;

internal static class GetTenantEndpoint
{
    public static void Map(IEndpointRouteBuilder tenants) => tenants
        .MapGet("/{tenantId:guid}", HandleAsync)
        .WithName("GetTenant")
        .WithSummary("Get a tenant")
        .WithDescription("One tenant with its profile, status and `version` (also the `ETag`).")
        .RequireAction(TenancyActions.TenantRead)
        .RunsUnderRouteTenant()
        .ProducesErrors(TenancyErrors.NotFound);

    private static async Task<Results<Ok<TenantResponse>, ProblemHttpResult>> HandleAsync(
        Guid tenantId,
        GetTenantHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tenantId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
