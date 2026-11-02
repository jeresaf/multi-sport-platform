using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats.GetFormat;

internal static class GetFormatEndpoint
{
    private const string _description =
        "A published format (docs/domain/formats.md) at `version`, the latest when left out: compatible contest shapes, " +
        "participant range, stages and their generators, and the parameters with type, bounds and default. A published " +
        "version never changes. Not audited (global configuration).";

    private const string _etag =
        "The format's `contentHash`, quoted: it changes only with a new version.";

    public static void MapAdmin(IEndpointRouteBuilder formats) =>
        Map(formats, "GetFormat", SportsConfigurationActions.FormatRead);

    public static void MapPlatform(IEndpointRouteBuilder formats) =>
        Map(formats, "GetPlatformFormat", SportsConfigurationActions.PlatformSportRead);

    private static void Map(IEndpointRouteBuilder formats, string name, ActionDefinition action) => formats
        .MapGet("/{code}", HandleAsync)
        .WithName(name)
        .WithSummary("Get a format")
        .WithDescription(_description)
        .DocumentsQueryInteger("version", minimum: 1)
        .RequireAction(action)
        .ValidateRequest<VersionRequest>(CommonErrors.InvalidQuery)
        .ReturnsETag(_etag)
        .ProducesErrors(SportsConfigurationErrors.FormatDefinitionNotFound);

    private static async Task<Results<Ok<FormatDefinitionResponse>, ProblemHttpResult>> HandleAsync(
        string code,
        [AsParameters] VersionRequest request,
        GetFormatHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(code, request.ToVersion(), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        response.Headers.ETag = $"\"{result.Value.ContentHash}\"";
        return TypedResults.Ok(result.Value);
    }
}
