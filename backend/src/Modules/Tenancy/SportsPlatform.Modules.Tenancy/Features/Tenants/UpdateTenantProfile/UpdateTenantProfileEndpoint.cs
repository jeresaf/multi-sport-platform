using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;

internal static class UpdateTenantProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder tenants) => tenants
        .MapPatch("/{tenantId:guid}", HandleAsync)
        .WithName("UpdateTenantProfile")
        .WithSummary("Change a tenant's profile")
        .WithDescription(
            "Changes the fields sent; missing or null fields stay as they are. The slug never changes, and the status " +
            "changes only through the status actions. Send `version` from the last read to get " +
            "409 TENANT_CONCURRENCY_CONFLICT instead of overwriting someone else's change; `If-Match` with the `ETag` " +
            "works the same way (if both are sent, both must match). The version is the tenant row's only: a settings " +
            "change made since your read (here or through the admin settings endpoint) is not detected.")
        .RequireAction(TenancyActions.TenantUpdate)
        .RunsUnderRouteTenant()
        .ValidateRequest<UpdateTenantProfileRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<TenantResponse>, ProblemHttpResult>> HandleAsync(
        Guid tenantId,
        UpdateTenantProfileRequest request,
        UpdateTenantProfileHandler handler,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!TenantVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(tenantId, request, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
