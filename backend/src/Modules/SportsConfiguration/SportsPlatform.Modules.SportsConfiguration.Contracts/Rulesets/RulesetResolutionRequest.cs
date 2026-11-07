using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// What to resolve: a template (its latest version when <see cref="TemplateVersion"/> is null), one of its formats, and
/// <see cref="Overrides"/> keyed by the template's editable paths (<c>standings.points.win</c>). Paths left out keep the
/// template's values; format parameters left out take the format's defaults.
/// </summary>
public sealed record RulesetResolutionRequest(
    string TemplateCode,
    int? TemplateVersion,
    RulesetFormatSelection Format,
    IReadOnlyDictionary<string, ParameterValue> Overrides);

/// <summary>A format of the template, at <see cref="Version"/> (the latest when null), with its parameters by code.</summary>
public sealed record RulesetFormatSelection(string Code, int? Version, IReadOnlyDictionary<string, ParameterValue> Parameters);
