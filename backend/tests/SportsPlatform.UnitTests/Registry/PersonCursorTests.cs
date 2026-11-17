using System.Buffers.Text;
using System.Text;
using SportsPlatform.Modules.Registry.Features.People;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>The people directory's cursor (S06 D22): the last person's sort key, opaque to clients.</summary>
public sealed class PersonCursorTests
{
    private static readonly Guid _id = new("0192a1b2-0000-7000-8000-00000000d001");

    [Theory]
    [InlineData("ssekandi", "john")]
    [InlineData("okello owino", "mary anne")]
    [InlineData("nänsubuga", "zoë")]
    public void A_cursor_survives_encoding(string lastNameKey, string firstNameKey)
    {
        var cursor = new PersonCursor(lastNameKey, firstNameKey, _id);

        var text = cursor.Encode();

        text.ShouldMatch("^[A-Za-z0-9_-]+$");
        PersonCursor.TryDecode(text, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    // A page that ends on the longest names in a three-byte script must still give a cursor the list accepts.
    [Fact]
    public void A_cursor_of_the_longest_keys_survives_encoding()
    {
        var cursor = new PersonCursor(new string('አ', 100), new string('ም', 100), _id);

        PersonCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("bm90LWEtY3Vyc29y")] // "not-a-cursor"
    public void Text_that_is_not_a_cursor_is_refused(string? text) => PersonCursor.TryDecode(text, out _).ShouldBeFalse();

    [Theory]
    [InlineData("ssekandi\njohn")] // no ID
    [InlineData("ssekandi\njohn\nnot-an-id")]
    [InlineData("\njohn\n0192a1b200007000800000000000d001")] // no last name key
    [InlineData("ssekandi\n\n0192a1b200007000800000000000d001")]
    [InlineData("ssekandi\njohn\n0192a1b200007000800000000000d001\nmore")]
    [InlineData("ssekandi\njohn\n0192A1B200007000800000000000D001")] // not as encoded
    public void A_cursor_with_other_content_is_refused(string content) =>
        PersonCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(content)), out _).ShouldBeFalse();

    [Fact]
    public void A_cursor_longer_than_any_sort_key_or_with_bytes_that_are_not_text_is_refused()
    {
        PersonCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new string('a', 800))), out _).ShouldBeFalse();
        PersonCursor.TryDecode(Base64Url.EncodeToString([0xC3, 0x28, 0x0A, 0x61, 0x0A]), out _).ShouldBeFalse();
    }
}
