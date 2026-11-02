namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The format definition files embedded in this assembly (<c>Definitions/Formats/*.yaml</c>) and their JSON Schema.
/// Singleton; tests replace it to serve other formats.
/// </summary>
public class FormatDefinitionFiles
{
    public const string SchemaFile = "format-definition.schema.json";

    private const string _folder = "Formats";

    /// <summary>The format files, e.g. <c>round-robin.yaml</c>, in name order.</summary>
    public virtual IReadOnlyList<string> Names { get; } = EmbeddedDefinitions.Names(_folder);

    /// <summary>The text of a format file.</summary>
    public virtual string Read(string file) => EmbeddedDefinitions.Read(_folder, file);

    /// <summary>The JSON Schema every format file is checked against; never replaced.</summary>
    public static string Schema() => EmbeddedDefinitions.Read(_folder, SchemaFile);
}
