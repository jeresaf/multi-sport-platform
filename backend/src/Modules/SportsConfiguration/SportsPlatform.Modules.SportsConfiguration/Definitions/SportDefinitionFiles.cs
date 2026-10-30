namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The sport definition files embedded in this assembly (<c>Definitions/Sports/*.yaml</c>) and their JSON Schema.
/// Singleton; tests replace it to serve other definitions.
/// </summary>
public class SportDefinitionFiles
{
    public const string SchemaFile = "sport-definition.schema.json";

    private const string _resourcePrefix = "SportsPlatform.Modules.SportsConfiguration.Definitions.Sports.";

    /// <summary>The definition files, e.g. <c>football.yaml</c>, in name order.</summary>
    public virtual IReadOnlyList<string> Names { get; } = [.. typeof(SportDefinitionFiles).Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(_resourcePrefix, StringComparison.Ordinal) && name.EndsWith(".yaml", StringComparison.Ordinal))
        .Select(name => name[_resourcePrefix.Length..])
        .Order(StringComparer.Ordinal)];

    /// <summary>The text of a definition file.</summary>
    public virtual string Read(string file) => ReadEmbedded(file);

    /// <summary>The JSON Schema every definition file is checked against; never replaced.</summary>
    public static string Schema() => ReadEmbedded(SchemaFile);

    private static string ReadEmbedded(string file)
    {
        using var stream = typeof(SportDefinitionFiles).Assembly.GetManifestResourceStream(_resourcePrefix + file)
            ?? throw new InvalidOperationException($"Sport definition file {file} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
