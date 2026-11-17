using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Registry.Features.People.GetPersonPrivate;

internal static class GetPersonPrivateEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapGet("/{personId:guid}/private", HandleAsync)
        .WithName("GetPersonPrivate")
        .WithSummary("Get a person's private data")
        .WithDescription("The person's full date of birth. Every read is audited, naming the person.")
        .RequireAction(RegistryActions.PersonReadPrivate)
        .ProducesErrors(RegistryErrors.PersonNotFound);

    private static async Task<Results<Ok<PersonPrivateResponse>, ProblemHttpResult>> HandleAsync(
        Guid personId,
        GetPersonPrivateHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(personId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // Private data is never stored by a browser or a proxy.
        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(result.Value);
    }
}
