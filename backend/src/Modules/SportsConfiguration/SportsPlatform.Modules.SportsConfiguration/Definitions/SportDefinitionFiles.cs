namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The sport definition files embedded in this assembly (<c>Definitions/Sports/*.yaml</c>) and their JSON Schema.
/// Singleton; tests replace it to serve other definitions.
/// </summary>
public class SportDefinitionFiles
{
    public const string SchemaFile = "sport-definition.schema.json";

    private const string _folder = "Sports";

    /// <summary>The definition files, e.g. <c>football.yaml</c>, in name order.</summary>
    public virtual IReadOnlyList<string> Names { get; } = EmbeddedDefinitions.Names(_folder);

    /// <summary>The text of a definition file.</summary>
    public virtual string Read(string file) => EmbeddedDefinitions.Read(_folder, file);

    /// <summary>The JSON Schema every definition file is checked against; never replaced.</summary>
    public static string Schema() => EmbeddedDefinitions.Read(_folder, SchemaFile);
}
