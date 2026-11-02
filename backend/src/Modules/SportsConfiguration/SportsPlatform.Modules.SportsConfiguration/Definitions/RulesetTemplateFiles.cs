namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The ruleset template files embedded in this assembly (<c>Definitions/Rulesets/*.yaml</c>, the tiebreaker catalogue
/// aside) and their JSON Schema. Singleton; tests replace it to serve other templates.
/// </summary>
public class RulesetTemplateFiles
{
    public const string SchemaFile = "ruleset-template.schema.json";

    internal const string Folder = "Rulesets";

    /// <summary>The template files, e.g. <c>football-league.yaml</c>, in name order.</summary>
    public virtual IReadOnlyList<string> Names { get; } =
        [.. EmbeddedDefinitions.Names(Folder).Where(name => name != TiebreakerCatalogue.File)];

    /// <summary>The text of a template file.</summary>
    public virtual string Read(string file) => EmbeddedDefinitions.Read(Folder, file);

    /// <summary>The JSON Schema every template file is checked against; never replaced.</summary>
    public static string Schema() => EmbeddedDefinitions.Read(Folder, SchemaFile);
}
