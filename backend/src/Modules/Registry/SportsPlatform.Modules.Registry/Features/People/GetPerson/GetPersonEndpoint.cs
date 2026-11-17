using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Registry.Features.People.GetPerson;

internal static class GetPersonEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapGet("/{personId:guid}", HandleAsync)
        .WithName("GetPerson")
        .WithSummary("Get a person")
        .WithDescription(
            "A person of the tenant named by `X-Tenant-Id`, whatever their status: names, aliases, external identifiers, " +
            "the birth year, `isMinor` and `ageStatus` on the tenant's date, and `version` (also the `ETag`). The date of " +
            "birth is in the private read.")
        .RequireAction(RegistryActions.PersonRead)
        .ReturnsETag()
        .ProducesErrors(RegistryErrors.PersonNotFound);

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> HandleAsync(
        Guid personId,
        GetPersonHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(personId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
