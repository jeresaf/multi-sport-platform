using System.Text.Json;
using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The tiebreaker catalogue (<c>Definitions/Rulesets/tiebreakers.yaml</c>, docs/domain/rulesets.md): the codes a
/// template's standings may list, and the sports each applies to. Embedded and checked at startup, not seeded: a code
/// never changes meaning. Singleton; tests replace it to serve another catalogue.
/// </summary>
public class TiebreakerCatalogue
{
    public const string File = "tiebreakers.yaml";

    public const string SchemaFile = "tiebreakers.schema.json";

    private static readonly DefinitionSchema _schema = new(() => EmbeddedDefinitions.Read(RulesetTemplateFiles.Folder, SchemaFile));

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly Lazy<IReadOnlyList<TiebreakerDefinition>> _all;

    public TiebreakerCatalogue() => _all = new(() => Load(Text()));

    /// <summary>The catalogue in the file's order; throws when the file has problems (<see cref="Validate"/>).</summary>
    public IReadOnlyList<TiebreakerDefinition> All => _all.Value;

    /// <summary>The catalogue's text.</summary>
    public virtual string Text() => EmbeddedDefinitions.Read(RulesetTemplateFiles.Folder, File);

    /// <summary>Every problem in the catalogue; empty when it is valid.</summary>
    public IReadOnlyList<DefinitionProblem> Validate() => Problems(Text());

    private static List<DefinitionProblem> Problems(string yaml)
    {
        var root = _schema.Read(File, yaml, out var problems);
        if (root is null)
        {
            return [.. problems];
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        var tiebreakers = root["tiebreakers"]!.AsArray();
        List<DefinitionProblem> found = [];
        for (var index = 0; index < tiebreakers.Count; index++)
        {
            var code = tiebreakers[index]!["code"]!.GetValue<string>();
            if (!codes.Add(code))
            {
                found.Add(new DefinitionProblem(File, $"$.tiebreakers[{index}].code", $"duplicate tiebreaker code \"{code}\""));
            }
        }

        return found;
    }

    private static List<TiebreakerDefinition> Load(string yaml)
    {
        var problems = Problems(yaml);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"The tiebreaker catalogue is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        }

        return YamlJson.Parse(yaml)!["tiebreakers"].Deserialize<List<TiebreakerDefinition>>(_options)
            ?? throw new JsonException("The tiebreaker catalogue is empty.");
    }
}
