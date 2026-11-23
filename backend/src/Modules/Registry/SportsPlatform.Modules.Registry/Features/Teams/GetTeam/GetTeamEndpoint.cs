using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Registry.Features.Teams.GetTeam;

internal static class GetTeamEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapGet("/{teamId:guid}", HandleAsync)
        .WithName("GetTeam")
        .WithSummary("Get a team")
        .WithDescription(
            "A team of the tenant named by `X-Tenant-Id`, whatever its status: variant, category (`genderCategory`, " +
            "`ageGroup`, `level`), name and short name, `nameSince` (the day the name began), slug, code, motto, colours, " +
            "founded year, organisation (id, display name, slug) and home venue (id, name, slug), each whatever its status " +
            "now, status and `version` (also the `ETag`).")
        .RequireAction(RegistryActions.TeamRead)
        .ReturnsETag()
        .ProducesErrors(RegistryErrors.TeamNotFound);

    private static async Task<Results<Ok<TeamResponse>, ProblemHttpResult>> HandleAsync(
        Guid teamId,
        GetTeamHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(teamId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}
