using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>
/// A <see cref="ParameterValue"/> as its bare JSON value: a whole number, <c>true</c>/<c>false</c>, a string or an array
/// of strings.
/// </summary>
public sealed class ParameterValueJsonConverter : JsonConverter<ParameterValue>
{
    private const string _expected = "A parameter value is a whole number, true or false, a code, or a list of codes.";

    public override ParameterValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Number when reader.TryGetInt32(out var number) => ParameterValue.Of(number),
        JsonTokenType.True or JsonTokenType.False => ParameterValue.Of(reader.GetBoolean()),
        JsonTokenType.String when !string.IsNullOrWhiteSpace(reader.GetString()) => ParameterValue.Of(reader.GetString()!),
        JsonTokenType.StartArray => ParameterValue.Of(ReadItems(ref reader)),
        _ => throw new JsonException(_expected),
    };

    public override void Write(Utf8JsonWriter writer, ParameterValue value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        switch (value.Type)
        {
            case ParameterType.Number:
                writer.WriteNumberValue(value.Number);
                break;
            case ParameterType.Flag:
                writer.WriteBooleanValue(value.Flag);
                break;
            case ParameterType.Choice:
                writer.WriteStringValue(value.Choice);
                break;
            case ParameterType.List:
                writer.WriteStartArray();
                foreach (var item in value.Items)
                {
                    writer.WriteStringValue(item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"Unknown parameter type {value.Type}.");
        }
    }

    private static List<string> ReadItems(ref Utf8JsonReader reader)
    {
        var items = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String || string.IsNullOrWhiteSpace(reader.GetString()))
            {
                throw new JsonException(_expected);
            }

            items.Add(reader.GetString()!);
        }

        return items;
    }
}
