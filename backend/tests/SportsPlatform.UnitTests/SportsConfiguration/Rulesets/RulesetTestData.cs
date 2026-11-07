using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Hashing;
using SportsPlatform.UnitTests.Engines;
using SportsPlatform.UnitTests.SportsConfiguration.Generators;

namespace SportsPlatform.UnitTests.SportsConfiguration.Rulesets;

/// <summary>
/// The embedded football definition, formats and templates behind in-memory reads, as the stored rows would serve them
/// (the content hash set), so the resolver runs without a database.
/// </summary>
internal static class RulesetTestData
{
    private static readonly RulesetTemplateFiles _files = new();

    public static SportDefinition Football { get; } = EngineTestData.Football with
    {
        ContentHash = CanonicalJson.Hash(YamlJson.Parse(new SportDefinitionFiles().Read("football.yaml"))),
    };

    public static RulesetTemplate League { get; } = Read("football-league.yaml");

    public static RulesetTemplate Cup { get; } = Read("football-cup.yaml");

    public static IReadOnlyList<FormatDefinition> Formats { get; } =
        [GeneratorTestData.RoundRobin, GeneratorTestData.Knockout, GeneratorTestData.SingleContest];

    public static IReadOnlyList<TiebreakerDefinition> Tiebreakers { get; } = new TiebreakerCatalogue().All;

    public static RulesetResolver Resolver(
        IEnumerable<RulesetTemplate>? templates = null, IEnumerable<SportDefinition>? sports = null, IEnumerable<FormatDefinition>? formats = null) =>
        new(new TemplateReads([.. templates ?? [League, Cup]]), new FormatReads([.. formats ?? Formats]), new SportReads([.. sports ?? [Football]]));

    public static RulesetResolutionRequest Request(
        string template = "FOOTBALL_LEAGUE",
        string format = "ROUND_ROBIN",
        int? formatVersion = null,
        IEnumerable<(string Code, ParameterValue Value)>? parameters = null,
        params (string Path, ParameterValue Value)[] overrides) =>
        new(
            template,
            null,
            new RulesetFormatSelection(format, formatVersion, (parameters ?? []).ToDictionary(p => p.Code, p => p.Value, StringComparer.Ordinal)),
            overrides.ToDictionary(o => o.Path, o => o.Value, StringComparer.Ordinal));

    public static async Task<RulesSnapshot> ResolveAsync(this RulesetResolver resolver, RulesetResolutionRequest request)
    {
        var resolution = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);
        resolution.Problems.ShouldBeEmpty();
        return resolution.Snapshot!;
    }

    /// <summary>A template with one more parameter a competition may change (what the football files don't allow).</summary>
    public static RulesetTemplate WithEditable(this RulesetTemplate template, string path, ParameterDefinition parameter) =>
        template with { Editable = new Dictionary<string, ParameterDefinition>(template.Editable, StringComparer.Ordinal) { [path] = parameter } };

    private static RulesetTemplate Read(string file)
    {
        var content = YamlJson.Parse(_files.Read(file))!;
        return RulesetTemplateContent.Read(content) with { ContentHash = CanonicalJson.Hash(content) };
    }

    private sealed class TemplateReads(IReadOnlyList<RulesetTemplate> all) : IRulesetTemplates
    {
        public IReadOnlyList<TiebreakerDefinition> Tiebreakers => RulesetTestData.Tiebreakers;

        public Task<IReadOnlyList<RulesetTemplate>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(all);

        public Task<RulesetTemplate?> GetAsync(string code, int? version, CancellationToken cancellationToken) =>
            Task.FromResult(Latest(all.Where(t => t.Code == code && (version is null || t.Version == version)), t => t.Version));
    }

    private sealed class FormatReads(IReadOnlyList<FormatDefinition> all) : IFormatDefinitions
    {
        public Task<IReadOnlyList<FormatDefinition>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(all);

        public Task<FormatDefinition?> GetAsync(string code, int? version, CancellationToken cancellationToken) =>
            Task.FromResult(Latest(all.Where(f => f.Code == code && (version is null || f.Version == version)), f => f.Version));
    }

    private sealed class SportReads(IReadOnlyList<SportDefinition> all) : ISportDefinitions
    {
        public Task<IReadOnlyList<SportVariant>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SportDefinition?> GetAsync(string variant, int? version, CancellationToken cancellationToken) =>
            Task.FromResult(Latest(all.Where(s => s.Variant == variant && (version is null || s.Version == version)), s => s.Version));
    }

    private static T? Latest<T>(IEnumerable<T> items, Func<T, int> version)
        where T : class => items.OrderBy(version).LastOrDefault();
}
