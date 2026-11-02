using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// What a ruleset template refers to: the sport definitions by variant and version, the formats by code (latest version
/// last) and the tiebreaker catalogue. The validator reads the embedded files, not the database, so the startup check,
/// the seeder and the tests agree. Only valid files count: an invalid one fails its own check first.
/// </summary>
public sealed class RulesetReferences(
    IEnumerable<SportDefinition> sports,
    IEnumerable<FormatDefinition> formats,
    IReadOnlyList<TiebreakerDefinition> tiebreakers)
{
    private readonly Dictionary<(string Variant, int Version), SportDefinition> _sports =
        sports.ToDictionary(sport => (sport.Variant, sport.Version));

    private readonly Dictionary<string, FormatDefinition> _formats = formats
        .GroupBy(format => format.Code, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.MaxBy(format => format.Version)!, StringComparer.Ordinal);

    private readonly Dictionary<string, TiebreakerDefinition> _tiebreakers =
        tiebreakers.ToDictionary(tiebreaker => tiebreaker.Code, StringComparer.Ordinal);

    public static RulesetReferences FromFiles(SportDefinitionFiles sports, FormatDefinitionFiles formats, TiebreakerCatalogue tiebreakers)
    {
        ArgumentNullException.ThrowIfNull(sports);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(tiebreakers);
        return new RulesetReferences(
            sports.Names
                .Select(file => sports.Read(file))
                .Where(yaml => SportDefinitionValidator.Validate(string.Empty, yaml).Count == 0)
                .Select(yaml => SportDefinitionContent.Read(YamlJson.Parse(yaml)!)),
            formats.Names
                .Select(file => formats.Read(file))
                .Where(yaml => FormatDefinitionValidator.Validate(string.Empty, yaml).Count == 0)
                .Select(yaml => FormatDefinitionContent.Read(YamlJson.Parse(yaml)!)),
            tiebreakers.Validate().Count == 0 ? tiebreakers.All : []);
    }

    public SportDefinition? Sport(string variant, int version) => _sports.GetValueOrDefault((variant, version));

    /// <summary>A format at its latest version.</summary>
    public FormatDefinition? Format(string code) => _formats.GetValueOrDefault(code);

    public TiebreakerDefinition? Tiebreaker(string code) => _tiebreakers.GetValueOrDefault(code);
}
