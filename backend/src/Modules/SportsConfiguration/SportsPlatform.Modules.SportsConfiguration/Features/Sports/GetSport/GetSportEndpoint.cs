using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.GetSport;

internal static class GetSportEndpoint
{
    private const string _description =
        "A variant's published definition (docs/domain/sports/README.md) at `version`, the latest when left out: " +
        "terminology, periods, actor roles, event types and the generic event types every definition has, positions, " +
        "staff and official roles, lineup, statistics and the scorer pad. A published version never changes. Labels are " +
        "the definition's; show them as given. Not audited (global configuration).";

    private const string _etag =
        "The definition's `contentHash`, quoted: it changes only with a new version.";

    public static void MapAdmin(IEndpointRouteBuilder sports) =>
        Map(sports, "GetSport", SportsConfigurationActions.SportRead);

    public static void MapPlatform(IEndpointRouteBuilder sports) =>
        Map(sports, "GetPlatformSport", SportsConfigurationActions.PlatformSportRead);

    private static void Map(IEndpointRouteBuilder sports, string name, ActionDefinition action) => sports
        .MapGet("/{variant}", HandleAsync)
        .WithName(name)
        .WithSummary("Get a sport definition")
        .WithDescription(_description)
        .DocumentsQueryInteger("version", minimum: 1)
        .RequireAction(action)
        .ValidateRequest<GetSportRequest>(CommonErrors.InvalidQuery)
        .ReturnsETag(_etag)
        .ProducesErrors(SportsConfigurationErrors.SportDefinitionNotFound);

    private static async Task<Results<Ok<SportDefinitionResponse>, ProblemHttpResult>> HandleAsync(
        string variant,
        [AsParameters] GetSportRequest request,
        GetSportHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(variant, request.ToVersion(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        response.Headers.ETag = $"\"{result.Value.ContentHash}\"";
        return TypedResults.Ok(result.Value);
    }
}
