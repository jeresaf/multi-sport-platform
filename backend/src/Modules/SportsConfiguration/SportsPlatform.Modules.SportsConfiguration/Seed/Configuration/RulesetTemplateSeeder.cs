using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

/// <summary>
/// The embedded ruleset templates (S05 D4, D11), as the owner: inserted by code and version, never changed
/// (<see cref="DefinitionSeeder{TRow}"/>; <c>RULESET_TEMPLATE_IMMUTABLE</c>, <c>RULESET_TEMPLATE_DUPLICATE_VERSION</c>).
/// A template is checked against the embedded sport definitions, formats and tiebreaker catalogue, which the earlier
/// seeders store from the same files.
/// </summary>
public sealed class RulesetTemplateSeeder(
    RulesetTemplateFiles files,
    SportDefinitionFiles sports,
    FormatDefinitionFiles formats,
    TiebreakerCatalogue tiebreakers,
    TimeProvider timeProvider)
    : DefinitionSeeder<PublishedRulesetTemplate>(timeProvider)
{
    public const string SeederKey = "config.configuration.rulesets";

    public const string ImmutableCode = "RULESET_TEMPLATE_IMMUTABLE";

    public const string DuplicateVersionCode = "RULESET_TEMPLATE_DUPLICATE_VERSION";

    private RulesetReferences? _references;

    public override string Key => SeederKey;

    // After sports and formats (D4).
    public override int Order => 2;

    protected override string ErrorPrefix => "RULESET_TEMPLATE";

    protected override string Kind => "Ruleset templates";

    private RulesetReferences References => _references ??= RulesetReferences.FromFiles(sports, formats, tiebreakers);

    protected override IEnumerable<(string File, string Yaml)> ReadFiles() => files.Names.Select(file => (file, files.Read(file)));

    protected override IReadOnlyList<DefinitionProblem> Validate(string file, string yaml) =>
        RulesetTemplateValidator.Validate(file, yaml, References);

    protected override PublishedRulesetTemplate ToRow(JsonNode content, string canonical, string hash, DateTimeOffset publishedAt)
    {
        var model = RulesetTemplateContent.Read(content);
        return new PublishedRulesetTemplate(
            model.Code, model.Version, model.Name, model.Sport.Variant, model.Sport.Version, model.Formats, canonical, hash, publishedAt);
    }
}
