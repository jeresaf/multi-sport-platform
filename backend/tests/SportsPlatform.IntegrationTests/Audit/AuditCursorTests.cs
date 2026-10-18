using SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;

namespace SportsPlatform.IntegrationTests.Audit;

/// <summary>The audit list's cursor (no database).</summary>
public sealed class AuditCursorTests
{
    [Fact]
    public void Cursor_round_trips_the_last_entry_as_UTC()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 17, 12, 30, 15, TimeSpan.FromHours(3)).AddTicks(1230);
        var id = Guid.CreateVersion7(occurredAt);

        var cursor = AuditCursor.Encode(occurredAt, id);

        cursor.Length.ShouldBe(32);
        cursor.ShouldNotContain(id.ToString());
        AuditCursor.TryDecode(cursor, out var decodedAt, out var decodedId).ShouldBeTrue();
        decodedAt.ShouldBe(occurredAt);
        decodedAt.Offset.ShouldBe(TimeSpan.Zero);
        decodedId.ShouldBe(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("01a14d28-2826-7000-9719-02ab7f16bc3d")] // a plain ID is not a cursor
    [InlineData("AZ_NKCgmcACXGQKrfxa8P")] // an outbox cursor
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA*")]
    [InlineData("_____________________________w==")] // padding
    [InlineData("f_____________________________-_")] // ticks beyond DateTimeOffset.MaxValue
    [InlineData("gAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // negative ticks
    public void Anything_else_is_not_a_cursor(string? cursor)
    {
        AuditCursor.TryDecode(cursor, out var occurredAt, out var id).ShouldBeFalse();
        occurredAt.ShouldBe(default);
        id.ShouldBe(Guid.Empty);
    }
}
