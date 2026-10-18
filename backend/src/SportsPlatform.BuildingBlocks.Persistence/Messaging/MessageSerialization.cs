using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>
/// JSON settings for outbox payloads, like the API (docs/architecture/api-conventions.md): camelCase members and enums
/// as UPPER_SNAKE strings. Enums are never numbers, so adding or reordering members can't change what a stored message
/// means; a number or an unknown name fails to read.
/// </summary>
public static class MessageSerialization
{
    /// <summary>How enum members are named in payloads, e.g. <c>RoundRobin</c> → <c>ROUND_ROBIN</c>.</summary>
    public static JsonNamingPolicy EnumNamingPolicy { get; } = JsonNamingPolicy.SnakeCaseUpper;

    // Set where the instance is created: options can't change after their first use.
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(EnumNamingPolicy, allowIntegerValues: false) },
    };
}
