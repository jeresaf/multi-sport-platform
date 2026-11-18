using System.Text;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Features.Media;

namespace SportsPlatform.UnitTests.Platform.Media;

public sealed class MediaRulesTests
{
    public static TheoryData<string, MediaFileType> KnownFiles => new()
    {
        { "jpeg", MediaFileType.Jpeg },
        { "png", MediaFileType.Png },
        { "webp", MediaFileType.WebP },
        { "pdf", MediaFileType.Pdf },
    };

    [Theory]
    [MemberData(nameof(KnownFiles))]
    public void Detects_the_type_by_the_first_bytes(string kind, MediaFileType expected) =>
        MediaRules.Detect(File(kind)).ShouldBe(expected);

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("MZ\u0090\0\u0003\0\0\0")]
    [InlineData("GIF89a")]
    [InlineData("RIFF\0\0\0\0WAVEfmt ")]
    [InlineData("")]
    public void Knows_no_other_type(string content) =>
        MediaRules.Detect(Encoding.Latin1.GetBytes(content)).ShouldBeNull();

    [Theory]
    [InlineData(MediaPurpose.PersonPhoto, "jpeg", true)]
    [InlineData(MediaPurpose.PersonPhoto, "png", true)]
    [InlineData(MediaPurpose.PersonPhoto, "webp", true)]
    [InlineData(MediaPurpose.PersonPhoto, "pdf", false)]
    [InlineData(MediaPurpose.ConsentEvidence, "pdf", true)]
    [InlineData(MediaPurpose.ConsentEvidence, "jpeg", true)]
    [InlineData(MediaPurpose.ConsentEvidence, "png", true)]
    [InlineData(MediaPurpose.ConsentEvidence, "webp", false)]
    public void Each_purpose_accepts_its_types(MediaPurpose purpose, string kind, bool accepted)
    {
        var result = MediaRules.CheckType(File(kind), purpose);

        result.IsSuccess.ShouldBe(accepted);
        if (!accepted)
        {
            result.Error!.Code.ShouldBe(PlatformErrors.MediaTypeNotAllowed.Code);
        }
    }

    [Theory]
    [InlineData(MediaPurpose.PersonPhoto, 5)]
    [InlineData(MediaPurpose.ConsentEvidence, 10)]
    public async Task Reads_up_to_the_limit_and_refuses_one_byte_more(MediaPurpose purpose, int megabytes)
    {
        var limit = megabytes * MediaRules.Megabyte;

        var atLimit = await MediaRules.ReadBoundedAsync(new MemoryStream(new byte[limit]), purpose, TestContext.Current.CancellationToken);
        var over = await MediaRules.ReadBoundedAsync(new MemoryStream(new byte[limit + 1]), purpose, TestContext.Current.CancellationToken);

        atLimit.Value.Length.ShouldBe((int)limit);
        over.IsFailure.ShouldBeTrue();
        over.Error!.Code.ShouldBe(PlatformErrors.MediaTooLarge.Code);
    }

    private static byte[] File(string kind) => kind switch
    {
        "jpeg" => TestImages.Jpeg(4, 4),
        "png" => TestImages.Png(4, 4),
        "webp" => TestImages.WebP(4, 4),
        _ => TestImages.Pdf(),
    };
}
