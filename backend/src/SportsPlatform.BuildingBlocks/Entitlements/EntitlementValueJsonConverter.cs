using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsPlatform.BuildingBlocks.Entitlements;

/// <summary>
/// The one JSON shape of an <see cref="EntitlementValue"/>, wherever it is written (the catalogue export, the API,
/// <c>jsonb</c> columns): <c>{ "enabled": true }</c>, <c>{ "limit": 120 }</c> (<c>null</c> = unlimited) or
/// <c>{ "level": "BASIC" }</c> (<c>null</c> = none). Typed by its one property, never a bare scalar.
/// </summary>
public sealed class EntitlementValueJsonConverter : JsonConverter<EntitlementValue>
{
    public const string Enabled = "enabled";
    public const string Limit = "limit";
    public const string Level = "level";

    public override EntitlementValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An entitlement value is an object with one property: enabled, limit or level.");
        }

        reader.Read();
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException("An entitlement value needs one property: enabled, limit or level.");
        }

        var property = reader.GetString();
        reader.Read();
        var value = property switch
        {
            Enabled when reader.TokenType is JsonTokenType.True or JsonTokenType.False => EntitlementValue.Flag(reader.GetBoolean()),
            Limit when reader.TokenType == JsonTokenType.Null => EntitlementValue.UnlimitedQuota,
            Limit when reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var limit) && limit >= 0 => EntitlementValue.Quota(limit),
            Level when reader.TokenType == JsonTokenType.Null => EntitlementValue.Level(null),
            Level when reader.TokenType == JsonTokenType.String && EntitlementDefinition.IsValidCode(reader.GetString()!) =>
                EntitlementValue.Level(reader.GetString()),
            _ => throw new JsonException(
                $"'{property}' is not an entitlement value: use enabled (true/false), limit (0 or more, null = unlimited) or level (a code, null = none)."),
        };

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("An entitlement value has exactly one property.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, EntitlementValue value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        switch (value.Type)
        {
            case EntitlementType.Boolean:
                writer.WriteBoolean(Enabled, value.Enabled);
                break;
            case EntitlementType.Quota when value.Limit is { } limit:
                writer.WriteNumber(Limit, limit);
                break;
            case EntitlementType.Quota:
                writer.WriteNull(Limit);
                break;
            case EntitlementType.Level:
                writer.WriteString(Level, value.LevelCode);
                break;
            default:
                throw new InvalidOperationException($"Unknown entitlement type {value.Type}.");
        }

        writer.WriteEndObject();
    }
}
