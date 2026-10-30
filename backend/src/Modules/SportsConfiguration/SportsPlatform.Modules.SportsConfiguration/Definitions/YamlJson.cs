using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Reads a YAML document into a JSON tree, so JSON Schema can check it (ADR-0006). Plain scalars are typed as in
/// YAML 1.2's core schema (null, true/false, integers, decimals); quoted scalars are always strings.
/// </summary>
public static partial class YamlJson
{
    /// <summary>The document's JSON tree. Throws <see cref="YamlException"/> on invalid YAML or a duplicate key.</summary>
    public static JsonNode? Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 1
            ? Convert(stream.Documents[0].RootNode)
            : throw new YamlException($"Expected one YAML document, found {stream.Documents.Count}.");
    }

    private static JsonNode? Convert(YamlNode node) => node switch
    {
        YamlMappingNode mapping => new JsonObject(mapping.Children.Select(child => KeyValuePair.Create(Key(child.Key), Convert(child.Value)))),
        YamlSequenceNode sequence => new JsonArray([.. sequence.Children.Select(Convert)]),
        YamlScalarNode scalar => Scalar(scalar),
        _ => throw new YamlException(node.Start, node.End, $"Unsupported YAML node {node.NodeType}."),
    };

    private static string Key(YamlNode key) => key is YamlScalarNode { Value: { } value }
        ? value
        : throw new YamlException(key.Start, key.End, "Mapping keys must be plain text.");

    private static JsonValue? Scalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style is not ScalarStyle.Plain)
        {
            return JsonValue.Create(value);
        }

        return value switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(true),
            "false" or "False" or "FALSE" => JsonValue.Create(false),
            // A number too large for long or decimal stays text, so the schema reports the wrong type at its path.
            _ when IntegerPattern().IsMatch(value) && long.TryParse(value, CultureInfo.InvariantCulture, out var integer) => JsonValue.Create(integer),
            _ when DecimalPattern().IsMatch(value) && decimal.TryParse(value, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            _ => JsonValue.Create(value),
        };
    }

    [GeneratedRegex("^[-+]?[0-9]+$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^[-+]?[0-9]+\.[0-9]+$")]
    private static partial Regex DecimalPattern();
}
