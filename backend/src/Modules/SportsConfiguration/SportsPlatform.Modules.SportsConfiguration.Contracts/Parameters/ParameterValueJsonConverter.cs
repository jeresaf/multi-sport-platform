using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

/// <summary>A <see cref="ParameterValue"/> as its bare JSON value: a whole number, <c>true</c>/<c>false</c> or a string.</summary>
public sealed class ParameterValueJsonConverter : JsonConverter<ParameterValue>
{
    public override ParameterValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Number when reader.TryGetInt32(out var number) => ParameterValue.Of(number),
        JsonTokenType.True or JsonTokenType.False => ParameterValue.Of(reader.GetBoolean()),
        JsonTokenType.String when !string.IsNullOrWhiteSpace(reader.GetString()) => ParameterValue.Of(reader.GetString()!),
        _ => throw new JsonException("A parameter value is a whole number, true or false, or a code."),
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
            default:
                throw new InvalidOperationException($"Unknown parameter type {value.Type}.");
        }
    }
}
