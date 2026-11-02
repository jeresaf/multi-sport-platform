using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats.ListFormats;

internal static class ListFormatsEndpoint
{
    private const string _description =
        "Every published format at its latest version, ordered by name, then code, in full: compatible contest shapes, " +
        "participant range, stages and their generators, and the parameters with type, bounds and default. Not paginated. " +
        "Not audited (global configuration).";

    public static void MapAdmin(IEndpointRouteBuilder formats) =>
        Map(formats, "ListFormats", SportsConfigurationActions.FormatRead);

    public static void MapPlatform(IEndpointRouteBuilder formats) =>
        Map(formats, "ListPlatformFormats", SportsConfigurationActions.PlatformSportRead);

    private static void Map(IEndpointRouteBuilder formats, string name, ActionDefinition action) => formats
        .MapGet("/", HandleAsync)
        .WithName(name)
        .WithSummary("List formats")
        .WithDescription(_description)
        .RequireAction(action);

    private static async Task<Ok<ListFormatsResponse>> HandleAsync(ListFormatsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
