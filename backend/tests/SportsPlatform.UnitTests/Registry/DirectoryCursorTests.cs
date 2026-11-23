using System.Buffers.Text;
using System.Text;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// The cursor of the registry directories (S07-T1b-2, shared from S07-T2b-2): the last record's sort key and ID, opaque
/// to clients.
/// </summary>
public sealed class DirectoryCursorTests
{
    private const int _maxLength = 120;

    private static readonly Guid _id = new("0192a1b2-0000-7000-8000-00000000e001");

    [Theory]
    [InlineData("kiwatule meadow ground")]
    [InlineData("ntinda swifts sc")]
    [InlineData("nsambya sportsplätz")]
    public void A_cursor_survives_encoding(string sortKey)
    {
        var cursor = new DirectoryCursor(sortKey, _id);

        var text = cursor.Encode();

        text.ShouldMatch("^[A-Za-z0-9_-]+$");
        DirectoryCursor.TryDecode(text, _maxLength, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    // A page that ends on the longest name in a three-byte script must still give a cursor the list accepts.
    [Fact]
    public void A_cursor_of_the_longest_key_survives_encoding()
    {
        var cursor = new DirectoryCursor(new string('አ', _maxLength), _id);

        DirectoryCursor.TryDecode(cursor.Encode(), _maxLength, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    // The directories that share the cursor decode it against their own name's length.
    [Fact]
    public void The_directories_sort_keys_are_at_most_120_characters()
    {
        VenueDetails.NameMaxLength.ShouldBe(_maxLength);
        OrganisationDetails.DisplayNameMaxLength.ShouldBe(_maxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor")]
    [InlineData("bm90LWEtY3Vyc29y")] // "not-a-cursor"
    public void Text_that_is_not_a_cursor_is_refused(string? text) =>
        DirectoryCursor.TryDecode(text, _maxLength, out _).ShouldBeFalse();

    [Theory]
    [InlineData("kiwatule meadow ground")] // no ID
    [InlineData("kiwatule meadow ground\nnot-an-id")]
    [InlineData("\n0192a1b200007000800000000000e001")] // no sort key
    [InlineData("kiwatule meadow ground\n0192a1b200007000800000000000e001\nmore")]
    [InlineData("kiwatule meadow ground\n0192A1B200007000800000000000E001")] // not as encoded
    public void A_cursor_with_other_content_is_refused(string content) =>
        DirectoryCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(content)), _maxLength, out _).ShouldBeFalse();

    [Fact]
    public void A_cursor_longer_than_the_directorys_sort_keys_or_with_bytes_that_are_not_text_is_refused()
    {
        var tooLong = new DirectoryCursor(new string('a', _maxLength + 1), _id).Encode();
        DirectoryCursor.TryDecode(tooLong, _maxLength, out _).ShouldBeFalse();
        DirectoryCursor.TryDecode(tooLong, _maxLength + 1, out _).ShouldBeTrue();
        DirectoryCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new string('a', 2000))), _maxLength, out _).ShouldBeFalse();
        DirectoryCursor.TryDecode(Base64Url.EncodeToString([0xC3, 0x28, 0x0A, 0x61]), _maxLength, out _).ShouldBeFalse();
    }
}
