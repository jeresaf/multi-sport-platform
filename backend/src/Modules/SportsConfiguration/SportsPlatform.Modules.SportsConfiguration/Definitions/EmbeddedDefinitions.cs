namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// The configuration files embedded in this assembly, by folder under <c>Definitions/</c> (<c>Sports</c>,
/// <c>Formats</c>): their YAML files and their JSON Schema.
/// </summary>
internal static class EmbeddedDefinitions
{
    private const string _namespace = "SportsPlatform.Modules.SportsConfiguration.Definitions.";

    /// <summary>The folder's <c>.yaml</c> files, e.g. <c>football.yaml</c>, in name order.</summary>
    public static IReadOnlyList<string> Names(string folder)
    {
        var prefix = Prefix(folder);
        return [.. typeof(EmbeddedDefinitions).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".yaml", StringComparison.Ordinal))
            .Select(name => name[prefix.Length..])
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>The text of one of the folder's files.</summary>
    public static string Read(string folder, string file)
    {
        using var stream = typeof(EmbeddedDefinitions).Assembly.GetManifestResourceStream(Prefix(folder) + file)
            ?? throw new InvalidOperationException($"Definition file {folder}/{file} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Prefix(string folder) => $"{_namespace}{folder}.";
}
