using System.Text.Json;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.UnitTests.BuildingBlocks;

// The outbox writes payloads with these options (ModuleDbContext.Publish) and consumers read them back with them.
public sealed class MessageSerializationTests
{
    [Fact]
    public void Enums_round_trip_as_UPPER_SNAKE_strings()
    {
        var published = new EditionFormatChosen.V1(Guid.CreateVersion7(), EditionFormat.RoundRobin);

        var json = JsonSerializer.Serialize(published, MessageSerialization.Options);

        json.ShouldContain("\"format\":\"ROUND_ROBIN\"");
        JsonSerializer.Deserialize<EditionFormatChosen.V1>(json, MessageSerialization.Options).ShouldBe(published);
    }

    // A number would silently change meaning when enum members are added or reordered.
    [Theory]
    [InlineData("1")]
    [InlineData("\"KNOCKOUT\"")]
    public void An_enum_number_or_unknown_name_does_not_read(string format)
    {
        var json = $$"""{"editionId":"0199f1a2-0000-7000-8000-000000000001","format":{{format}}}""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<EditionFormatChosen.V1>(json, MessageSerialization.Options));
    }

    public enum EditionFormat
    {
        League,
        RoundRobin,
    }

    public static class EditionFormatChosen
    {
        public sealed record V1(Guid EditionId, EditionFormat Format) : IIntegrationEvent;
    }
}
