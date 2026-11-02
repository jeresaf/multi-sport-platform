using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// A format's content as its typed model. The model refuses a key it doesn't map, so a schema change without a model
/// change fails the seeder and the tests rather than losing data.
/// </summary>
public static class FormatDefinitionContent
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
    };

    /// <summary>The typed model of a format's content (canonical JSON or any JSON of the same tree).</summary>
    public static FormatDefinition Read(string json) =>
        JsonSerializer.Deserialize<FormatDefinition>(json, _options)
        ?? throw new JsonException("A format definition's content is null.");

    /// <summary>The typed model of a format's JSON tree.</summary>
    public static FormatDefinition Read(JsonNode content) =>
        content.Deserialize<FormatDefinition>(_options) ?? throw new JsonException("A format definition's content is null.");
}
