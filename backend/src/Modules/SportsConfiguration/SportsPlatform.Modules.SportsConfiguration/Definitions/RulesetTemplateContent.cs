using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// A ruleset template's content as its typed model. Each editable parameter's default is read from the value at its
/// path (the file writes it once, in <c>values</c>). The model refuses a key it doesn't map, so a schema change without a
/// model change fails the seeder and the tests rather than losing data.
/// </summary>
public static class RulesetTemplateContent
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    /// <summary>The typed model of a template's content (canonical JSON or any JSON of the same tree).</summary>
    public static RulesetTemplate Read(string json) =>
        Read(JsonNode.Parse(json) ?? throw new JsonException("A ruleset template's content is null."));

    /// <summary>The typed model of a template's JSON tree.</summary>
    public static RulesetTemplate Read(JsonNode content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var copy = content.DeepClone().AsObject();
        var values = copy["values"] ?? throw new JsonException("A ruleset template has no values.");
        foreach (var (path, parameter) in copy["editable"]?.AsObject() ?? new JsonObject())
        {
            if (!TryGetValue(values, path, out var value) || value is null)
            {
                throw new JsonException($"The editable parameter {path} has no value.");
            }

            parameter!.AsObject()["default"] = value.DeepClone();
        }

        return copy.Deserialize<RulesetTemplate>(_options) ?? throw new JsonException("A ruleset template's content is null.");
    }

    /// <summary>The typed model of a template's <c>values</c>.</summary>
    public static RulesetValues ReadValues(JsonNode values) =>
        values.Deserialize<RulesetValues>(_options) ?? throw new JsonException("A ruleset template's values are null.");

    /// <summary>
    /// A template's <c>values</c> as the file writes them (snake_case keys, enums in upper snake case, nulls kept):
    /// <see cref="ReadValues"/> reads the tree back into equal values.
    /// </summary>
    public static JsonObject WriteValues(RulesetValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return JsonSerializer.SerializeToNode(values, _options)!.AsObject();
    }

    /// <summary>
    /// The node at a dotted path into a template's values (<c>standings.points.win</c>): false when a segment is missing;
    /// true with a null node for a JSON null.
    /// </summary>
    public static bool TryGetValue(JsonNode values, string path, out JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(path);
        value = values;
        foreach (var segment in path.Split('.'))
        {
            if (value is not JsonObject node || !node.TryGetPropertyValue(segment, out value))
            {
                value = null;
                return false;
            }
        }

        return true;
    }
}
