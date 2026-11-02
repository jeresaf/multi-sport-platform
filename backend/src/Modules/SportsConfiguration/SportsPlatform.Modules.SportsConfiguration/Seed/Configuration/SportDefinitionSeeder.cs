using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

/// <summary>
/// The embedded sport definitions (S05 D4), as the owner: inserted by variant and version, never changed
/// (<see cref="DefinitionSeeder{TRow}"/>; <c>SPORT_DEFINITION_IMMUTABLE</c>, <c>SPORT_DEFINITION_DUPLICATE_VERSION</c>).
/// </summary>
public sealed class SportDefinitionSeeder(SportDefinitionFiles files, TimeProvider timeProvider)
    : DefinitionSeeder<PublishedSportDefinition>(timeProvider)
{
    public const string SeederKey = "config.configuration.sports";

    public const string ImmutableCode = "SPORT_DEFINITION_IMMUTABLE";

    public const string DuplicateVersionCode = "SPORT_DEFINITION_DUPLICATE_VERSION";

    public override string Key => SeederKey;

    // Sports before formats and rulesets (D4).
    public override int Order => 0;

    protected override string ErrorPrefix => "SPORT_DEFINITION";

    protected override string Kind => "Sport definitions";

    protected override IEnumerable<(string File, string Yaml)> ReadFiles() => files.Names.Select(file => (file, files.Read(file)));

    protected override IReadOnlyList<DefinitionProblem> Validate(string file, string yaml) => SportDefinitionValidator.Validate(file, yaml);

    protected override PublishedSportDefinition ToRow(JsonNode content, string canonical, string hash, DateTimeOffset publishedAt)
    {
        var model = SportDefinitionContent.Read(content);
        return new PublishedSportDefinition(
            model.Variant, model.Sport, model.Version, model.Name, model.ScoringFamily, model.Shape, canonical, hash, publishedAt);
    }
}
