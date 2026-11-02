using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Formats;

namespace SportsPlatform.Modules.SportsConfiguration.Seed.Configuration;

/// <summary>
/// The embedded format definitions (S05 D4, D10), as the owner: inserted by code and version, never changed
/// (<see cref="DefinitionSeeder{TRow}"/>; <c>FORMAT_DEFINITION_IMMUTABLE</c>, <c>FORMAT_DEFINITION_DUPLICATE_VERSION</c>).
/// </summary>
public sealed class FormatDefinitionSeeder(FormatDefinitionFiles files, TimeProvider timeProvider)
    : DefinitionSeeder<PublishedFormatDefinition>(timeProvider)
{
    public const string SeederKey = "config.configuration.formats";

    public const string ImmutableCode = "FORMAT_DEFINITION_IMMUTABLE";

    public const string DuplicateVersionCode = "FORMAT_DEFINITION_DUPLICATE_VERSION";

    public override string Key => SeederKey;

    // After sports, before rulesets (D4).
    public override int Order => 1;

    protected override string ErrorPrefix => "FORMAT_DEFINITION";

    protected override string Kind => "Format definitions";

    protected override IEnumerable<(string File, string Yaml)> ReadFiles() => files.Names.Select(file => (file, files.Read(file)));

    protected override IReadOnlyList<DefinitionProblem> Validate(string file, string yaml) => FormatDefinitionValidator.Validate(file, yaml);

    protected override PublishedFormatDefinition ToRow(JsonNode content, string canonical, string hash, DateTimeOffset publishedAt)
    {
        var model = FormatDefinitionContent.Read(content);
        return new PublishedFormatDefinition(model.Code, model.Version, model.Name, canonical, hash, publishedAt);
    }
}
