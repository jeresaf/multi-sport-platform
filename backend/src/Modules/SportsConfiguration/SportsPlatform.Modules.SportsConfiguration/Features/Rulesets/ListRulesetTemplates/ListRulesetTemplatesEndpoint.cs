using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.ListRulesetTemplates;

internal static class ListRulesetTemplatesEndpoint
{
    private const string _description =
        "Every published ruleset template at its latest version, ordered by name, then code, in full: the sport variant and " +
        "version, the formats, the rules, the parameters a competition may change (type, bounds or values, default) and " +
        "the tiebreakers they name. Not paginated. Not audited (global configuration).";

    public static void MapAdmin(IEndpointRouteBuilder templates) =>
        Map(templates, "ListRulesetTemplates", SportsConfigurationActions.RulesetRead);

    public static void MapPlatform(IEndpointRouteBuilder templates) =>
        Map(templates, "ListPlatformRulesetTemplates", SportsConfigurationActions.PlatformSportRead);

    private static void Map(IEndpointRouteBuilder templates, string name, ActionDefinition action) => templates
        .MapGet("/", HandleAsync)
        .WithName(name)
        .WithSummary("List ruleset templates")
        .WithDescription(_description)
        .RequireAction(action);

    private static async Task<Ok<ListRulesetTemplatesResponse>> HandleAsync(ListRulesetTemplatesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
