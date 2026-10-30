using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// A definition's content as its typed model. The model refuses a key it doesn't map, so a schema change without a
/// model change fails the seeder and the tests rather than losing data.
/// </summary>
public static class SportDefinitionContent
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false), new ScalarTextDictionaryConverter() },
    };

    /// <summary>The typed model of a definition's content (canonical JSON or any JSON of the same tree).</summary>
    public static SportDefinition Read(string json) =>
        JsonSerializer.Deserialize<SportDefinition>(json, _options)
        ?? throw new JsonException("A sport definition's content is null.");

    /// <summary>The typed model of a definition's JSON tree.</summary>
    public static SportDefinition Read(JsonNode content) =>
        content.Deserialize<SportDefinition>(_options) ?? throw new JsonException("A sport definition's content is null.");

    // StatisticDerivation.Where: scalars as written ("true", "OFFENSIVE", "3"); the counted event's metadata field gives
    // the type. Anything else isn't a condition.
    private sealed class ScalarTextDictionaryConverter : JsonConverter<IReadOnlyDictionary<string, string>>
    {
        public override IReadOnlyDictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, value) in JsonNode.Parse(ref reader)?.AsObject() ?? throw new JsonException("Expected an object."))
            {
                values[name] = value?.GetValueKind() switch
                {
                    JsonValueKind.String => value.GetValue<string>(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToJsonString(),
                    _ => throw new JsonException($"The condition {name} must be a string, number or boolean."),
                };
            }

            return values;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);
            writer.WriteStartObject();
            foreach (var (name, text) in value)
            {
                writer.WriteString(name, text);
            }

            writer.WriteEndObject();
        }
    }
}
