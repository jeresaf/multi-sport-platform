using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports.ListSports;

internal static class ListSportsEndpoint
{
    private const string _description =
        "Every published sport variant at its latest version, ordered by name, then variant code: its sport, scoring " +
        "family, contest shape and latest version. Not paginated. Not audited (global configuration).";

    public static void MapAdmin(IEndpointRouteBuilder sports) =>
        Map(sports, "ListSports", SportsConfigurationActions.SportRead);

    public static void MapPlatform(IEndpointRouteBuilder sports) =>
        Map(sports, "ListPlatformSports", SportsConfigurationActions.PlatformSportRead);

    private static void Map(IEndpointRouteBuilder sports, string name, ActionDefinition action) => sports
        .MapGet("/", HandleAsync)
        .WithName(name)
        .WithSummary("List sport variants")
        .WithDescription(_description)
        .RequireAction(action);

    private static async Task<Ok<ListSportsResponse>> HandleAsync(ListSportsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
