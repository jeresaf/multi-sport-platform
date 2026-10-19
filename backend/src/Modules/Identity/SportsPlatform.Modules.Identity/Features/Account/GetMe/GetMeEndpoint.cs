using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Identity.Features.Account.GetMe;

internal static class GetMeEndpoint
{
    public static void Map(IEndpointRouteBuilder account) => account
        .MapGet("/me", HandleAsync)
        .WithName("GetMe")
        .WithSummary("Read the signed-in user")
        .WithDescription(
            "The signed-in user: ID, email, active platform roles, and the tenants they are an active member of with " +
            "their roles there (empty until memberships exist).")
        .RequireAction(IdentityActions.AccountRead);

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> HandleAsync(
        GetMeHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
