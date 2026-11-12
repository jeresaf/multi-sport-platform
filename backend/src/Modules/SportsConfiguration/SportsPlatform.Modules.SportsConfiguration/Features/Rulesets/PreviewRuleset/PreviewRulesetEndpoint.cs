using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.PreviewRuleset;

/// <summary>
/// <c>POST /api/admin/v1/ruleset-templates/{code}/preview</c> (<c>ruleset.read</c>): a read with a body. Nothing is
/// saved or audited (S05 D16).
/// </summary>
internal static class PreviewRulesetEndpoint
{
    private const string _description =
        "Resolves the ruleset template `code` at `version` (the latest when left out) with one of its formats and the " +
        "changes a competition would make, into the effective-rules snapshot an edition would freeze " +
        "(docs/domain/rulesets.md): the exact sport, format and template versions, the format's parameters with their " +
        "defaults, the rules and the snapshot's `hash`. The same template version, format version, parameters and " +
        "changes always give the same hash. `overrides` is keyed by the template's editable paths " +
        "(`standings.points.win`); paths left out keep the template's values. Nothing is saved or audited.\n\n" +
        "A rule or parameter the resolver refuses is 422 with the first problem's code (by field) and one `errors` " +
        "entry per field: `format.code`, `format.version`, `format.parameters.<code>`, `overrides.<path>`. A value " +
        "that isn't a whole number, true or false, a code or a list of codes is refused on its field like a value of " +
        "the wrong type. A missing format or format code, or another member, is 400 VALIDATION_INVALID_REQUEST.";

    public static void Map(IEndpointRouteBuilder templates) => templates
        .MapPost("/{code}/preview", HandleAsync)
        .WithName("PreviewRuleset")
        .WithSummary("Preview a ruleset's effective rules")
        .WithDescription(_description)
        .DocumentsQueryInteger("version", minimum: 1)
        .RequireAction(SportsConfigurationActions.RulesetRead)
        .ValidateRequest<VersionRequest>(CommonErrors.InvalidQuery)
        .AcceptsStrictJson<PreviewRulesetRequest>()
        .ProducesErrors(
            CommonErrors.InvalidRequest,
            SportsConfigurationErrors.RulesetTemplateNotFound,
            SportsConfigurationErrors.RulesetParameterUnknown,
            SportsConfigurationErrors.RulesetParameterOutOfRange,
            SportsConfigurationErrors.RulesetIncompatibleFormat,
            SportsConfigurationErrors.RulesetUnknownTiebreaker,
            SportsConfigurationErrors.RulesetTieResolutionUnsupported,
            SportsConfigurationErrors.FormatParameterInvalid);

    private static async Task<Results<Ok<RulesSnapshotResponse>, ProblemHttpResult>> HandleAsync(
        string code,
        [AsParameters] VersionRequest request,
        [FromBody] JsonElement body,
        PreviewRulesetHandler handler,
        IOptions<HttpJsonOptions> json,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!StrictJsonBody.TryRead<PreviewRulesetBody>(
                body, json.Value.SerializerOptions, CommonErrors.InvalidRequest, out var preview, out var unreadable))
        {
            return unreadable;
        }

        if (await httpContext.ValidateRequestAsync(preview, CommonErrors.InvalidRequest) is { } invalid)
        {
            return invalid;
        }

        var resolution = await handler.HandleAsync(code, request.ToVersion(), preview, cancellationToken);
        return resolution.Snapshot is { } snapshot
            ? TypedResults.Ok(RulesSnapshotResponse.From(snapshot))
            : ToProblem(resolution.Problems);
    }

    private static ProblemHttpResult ToProblem(IReadOnlyList<RulesetProblem> problems)
    {
        // The template is the path's code and the query's version: a missing one is not found, not a field of the body.
        if (problems.Any(problem => problem.Field is RulesetResolver.TemplateField or RulesetResolver.TemplateVersionField))
        {
            return SportsConfigurationErrors.RulesetTemplateNotFound.ToProblem();
        }

        // Ordered by field, one per field (RulesetResolution): the first one's code, every field in errors.
        var problem = problems[0].Error.ToProblem(string.Join(" ", problems.Select(p => $"{p.Field}: {p.Message}.")));
        problem.ProblemDetails.Extensions[ProblemFields.Errors] = problems
            .Select(p => new FieldError(p.Field, p.Error.Code))
            .ToList();
        return problem;
    }
}
