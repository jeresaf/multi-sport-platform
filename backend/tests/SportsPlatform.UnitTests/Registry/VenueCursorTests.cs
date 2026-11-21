using System.Buffers.Text;
using System.Text;
using SportsPlatform.Modules.Registry.Features.Venues;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>The venue directory's cursor (S07-T1b-2): the last venue's sort key and ID, opaque to clients.</summary>
public sealed class VenueCursorTests
{
    private static readonly Guid _id = new("0192a1b2-0000-7000-8000-00000000e001");

    [Theory]
    [InlineData("kiwatule meadow ground")]
    [InlineData("bukoto community stadium")]
    [InlineData("nsambya sportsplätz")]
    public void A_cursor_survives_encoding(string sortKey)
    {
        var cursor = new VenueCursor(sortKey, _id);

        var text = cursor.Encode();

        text.ShouldMatch("^[A-Za-z0-9_-]+$");
        VenueCursor.TryDecode(text, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    // A page that ends on the longest name in a three-byte script must still give a cursor the list accepts.
    [Fact]
    public void A_cursor_of_the_longest_key_survives_encoding()
    {
        var cursor = new VenueCursor(new string('አ', 120), _id);

        VenueCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("bm90LWEtY3Vyc29y")] // "not-a-cursor"
    public void Text_that_is_not_a_cursor_is_refused(string? text) => VenueCursor.TryDecode(text, out _).ShouldBeFalse();

    [Theory]
    [InlineData("kiwatule meadow ground")] // no ID
    [InlineData("kiwatule meadow ground\nnot-an-id")]
    [InlineData("\n0192a1b200007000800000000000e001")] // no sort key
    [InlineData("kiwatule meadow ground\n0192a1b200007000800000000000e001\nmore")]
    [InlineData("kiwatule meadow ground\n0192A1B200007000800000000000E001")] // not as encoded
    public void A_cursor_with_other_content_is_refused(string content) =>
        VenueCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(content)), out _).ShouldBeFalse();

    [Fact]
    public void A_cursor_longer_than_any_sort_key_or_with_bytes_that_are_not_text_is_refused()
    {
        VenueCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new string('a', 121) + "\n0192a1b200007000800000000000e001")), out _)
            .ShouldBeFalse();
        VenueCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new string('a', 500))), out _).ShouldBeFalse();
        VenueCursor.TryDecode(Base64Url.EncodeToString([0xC3, 0x28, 0x0A, 0x61]), out _).ShouldBeFalse();
    }
}
