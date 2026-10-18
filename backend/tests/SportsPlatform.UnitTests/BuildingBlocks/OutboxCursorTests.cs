using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class OutboxCursorTests
{
    [Fact]
    public void Cursor_round_trips_the_last_message_ID()
    {
        var id = SequentialIds.Next();

        var cursor = OutboxCursor.Encode(id);

        cursor.Length.ShouldBe(22);
        cursor.ShouldNotContain(id.ToString());
        OutboxCursor.TryDecode(cursor, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("01a14d28-2826-7000-9719-02ab7f16bc3d")] // a plain ID is not a cursor
    [InlineData("AZ_NKCgmcACXGQKrfxa8P=")] // padding
    [InlineData("AZ_NKCgmcACXGQKrfxa8P*")]
    public void Anything_else_is_not_a_cursor(string? cursor)
    {
        OutboxCursor.TryDecode(cursor, out var decoded).ShouldBeFalse();
        decoded.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Non_canonical_spelling_of_a_cursor_is_rejected()
    {
        var cursor = OutboxCursor.Encode(SequentialIds.Next());
        // The last character carries 2 unused bits; setting one decodes to the same 16 bytes.
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var last = alphabet.IndexOf(cursor[^1], StringComparison.Ordinal);
        var other = cursor[..^1] + alphabet[last ^ 1];

        OutboxCursor.TryDecode(other, out _).ShouldBeFalse();
    }
}
