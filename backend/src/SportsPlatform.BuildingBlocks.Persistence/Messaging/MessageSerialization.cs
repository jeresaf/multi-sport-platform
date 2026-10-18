using System.Text.Json;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>JSON settings for outbox payloads: camelCase, like the API.</summary>
public static class MessageSerialization
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
