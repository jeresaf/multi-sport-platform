using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.GetRulesetTemplate;

/// <summary>A ruleset template at a version, the latest by default.</summary>
public sealed class GetRulesetTemplateHandler(IRulesetTemplates templates)
{
    public async Task<Result<RulesetTemplateResponse>> HandleAsync(string code, int? version, CancellationToken cancellationToken)
    {
        var template = await templates.GetAsync(code, version, cancellationToken);
        return template is null
            ? SportsConfigurationErrors.RulesetTemplateNotFound
            : Result.Success(RulesetTemplateResponse.From(template, templates.Tiebreakers));
    }
}
