using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;

internal static class CreateTenantEndpoint
{
    public static void Map(IEndpointRouteBuilder tenants) => tenants
        .MapPost("/", HandleAsync)
        .WithName("CreateTenant")
        .WithSummary("Create a tenant")
        .WithDescription(
            "Creates a tenant in PROVISIONING. `slug` (3–40 lower-case letters, digits and hyphens, not a reserved word) " +
            "names its platform subdomain, never changes and is never reused, even after the tenant is archived. Country, " +
            "time zone, locale (`en` or `en-UG`) and currency must be active reference data. `publicEnabled` defaults to " +
            "false; the public site shows only while the tenant is ACTIVE.")
        .RequireAction(TenancyActions.TenantCreate)
        .ValidateRequest<CreateTenantRequest>(CommonErrors.InvalidRequest)
        .ProducesErrors(TenancyErrors.SlugTaken);

    private static async Task<Results<Created<TenantResponse>, ProblemHttpResult>> HandleAsync(
        CreateTenantRequest request,
        CreateTenantHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Created($"/api/platform/v1/tenants/{result.Value.Id}", result.Value);
    }
}
