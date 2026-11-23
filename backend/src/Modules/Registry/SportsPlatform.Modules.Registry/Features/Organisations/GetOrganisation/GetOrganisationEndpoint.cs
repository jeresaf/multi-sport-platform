using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;

internal static class GetOrganisationEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapGet("/{organisationId:guid}", HandleAsync)
        .WithName("GetOrganisation")
        .WithSummary("Get an organisation")
        .WithDescription(
            "An organisation of the tenant named by `X-Tenant-Id`, whatever its status: type, names, country, city, " +
            "founded year, website, the body's own email and phone, description, primary venue (id, name, slug), the " +
            "current `parent` (id, display name, slug, status; null without one), the current `children` (the same " +
            "summary, archived ones left out, sorted by display name ignoring case, accents and punctuation, as the " +
            "directory; `GET /organisations?parentId=` pages them), status " +
            "and `version` (also the `ETag`; it doesn't move when a child leaves).")
        .RequireAction(RegistryActions.OrganisationRead)
        .ReturnsETag()
        .ProducesErrors(RegistryErrors.OrganisationNotFound);

    private static async Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> HandleAsync(
        Guid organisationId,
        GetOrganisationHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(organisationId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
