using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.GetRulesetTemplate;

internal static class GetRulesetTemplateEndpoint
{
    private const string _description =
        "A published ruleset template (docs/domain/rulesets.md) at `version`, the latest when left out: the sport variant " +
        "and version, the formats, the rules, the parameters a competition may change (type, bounds or values, default) " +
        "and the tiebreakers they name. A published version never changes. Not audited (global configuration).";

    private const string _etag =
        "The template's `contentHash`, quoted: it changes only with a new version.";

    public static void MapAdmin(IEndpointRouteBuilder templates) =>
        Map(templates, "GetRulesetTemplate", SportsConfigurationActions.RulesetRead);

    public static void MapPlatform(IEndpointRouteBuilder templates) =>
        Map(templates, "GetPlatformRulesetTemplate", SportsConfigurationActions.PlatformSportRead);

    private static void Map(IEndpointRouteBuilder templates, string name, ActionDefinition action) => templates
        .MapGet("/{code}", HandleAsync)
        .WithName(name)
        .WithSummary("Get a ruleset template")
        .WithDescription(_description)
        .DocumentsQueryInteger("version", minimum: 1)
        .RequireAction(action)
        .ValidateRequest<VersionRequest>(CommonErrors.InvalidQuery)
        .ReturnsETag(_etag)
        .ProducesErrors(SportsConfigurationErrors.RulesetTemplateNotFound);

    private static async Task<Results<Ok<RulesetTemplateResponse>, ProblemHttpResult>> HandleAsync(
        string code,
        [AsParameters] VersionRequest request,
        GetRulesetTemplateHandler handler,
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
