using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Registry.Features.People.ListPersonStatusChanges;

internal static class ListPersonStatusChangesEndpoint
{
    public static void Map(IEndpointRouteBuilder people) => people
        .MapGet("/{personId:guid}/status-changes", HandleAsync)
        .WithName("ListPersonStatusChanges")
        .WithSummary("List a person's status history")
        .WithDescription("Every status the person has had, newest first, with who changed it, when and why.")
        .RequireAction(RegistryActions.PersonRead)
        .ProducesErrors(RegistryErrors.PersonNotFound);

    private static async Task<Results<Ok<ListPersonStatusChangesResponse>, ProblemHttpResult>> HandleAsync(
        Guid personId,
        ListPersonStatusChangesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(personId, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
