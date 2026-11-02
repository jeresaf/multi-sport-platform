using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets.ListRulesetTemplates;

/// <summary>Every published ruleset template at its latest version, by name, then code.</summary>
public sealed class ListRulesetTemplatesHandler(IRulesetTemplates templates)
{
    public async Task<ListRulesetTemplatesResponse> HandleAsync(CancellationToken cancellationToken) =>
        new([.. (await templates.ListAsync(cancellationToken)).Select(template => RulesetTemplateResponse.From(template, templates.Tiebreakers))]);
}

public sealed record ListRulesetTemplatesResponse(IReadOnlyList<RulesetTemplateResponse> Items);
