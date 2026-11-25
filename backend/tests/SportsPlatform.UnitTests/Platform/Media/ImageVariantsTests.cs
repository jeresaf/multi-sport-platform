using System.Text;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Features.Media;
using SportsPlatform.Modules.Platform.Infrastructure.Media;

namespace SportsPlatform.UnitTests.Platform.Media;

public sealed class ImageVariantsTests
{
    [Fact]
    public void A_large_photo_gets_three_WebP_variants_by_its_longest_side()
    {
        var variants = ImageVariants.Create(TestImages.Jpeg(2000, 1000), MediaPurpose.PersonPhoto).Value;

        variants.Select(v => (v.Variant, v.Width, v.Height)).ShouldBe(
        [
            (MediaVariant.Thumbnail, 160, 80),
            (MediaVariant.Medium, 640, 320),
            (MediaVariant.Large, 1600, 800),
        ]);
        foreach (var variant in variants)
        {
            Encoding.ASCII.GetString(variant.Bytes, 8, 4).ShouldBe("WEBP");
            TestImages.SizeOf(variant.Bytes).ShouldBe((variant.Width, variant.Height));
        }
    }

    [Fact]
    public void A_small_photo_is_never_scaled_up()
    {
        var variants = ImageVariants.Create(TestImages.Png(300, 400), MediaPurpose.PersonPhoto).Value;

        variants.Select(v => (v.Width, v.Height)).ShouldBe([(120, 160), (300, 400), (300, 400)]);
    }

    [Fact]
    public void Variants_carry_no_EXIF_data_such_as_a_GPS_position()
    {
        var upload = TestImages.WithExif(TestImages.Jpeg(800, 600), orientation: 1);
        ContainsBytes(upload, "Exif\0\0"u8.ToArray()).ShouldBeTrue();

        var variants = ImageVariants.Create(upload, MediaPurpose.PersonPhoto).Value;

        foreach (var variant in variants)
        {
            TestImages.WebPChunks(variant.Bytes).ShouldNotContain("EXIF");
            TestImages.WebPChunks(variant.Bytes).ShouldNotContain("XMP ");
            ContainsBytes(variant.Bytes, "Exif"u8.ToArray()).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(1, "red", 400, 200)]
    [InlineData(2, "green", 400, 200)]
    [InlineData(3, "yellow", 400, 200)]
    [InlineData(4, "blue", 400, 200)]
    [InlineData(5, "red", 200, 400)]
    [InlineData(6, "blue", 200, 400)]
    [InlineData(7, "yellow", 200, 400)]
    [InlineData(8, "green", 200, 400)]
    public void The_EXIF_orientation_is_applied(ushort orientation, string topLeft, int width, int height)
    {
        // The upload is 400×200 with red at the top left; orientation 6 means "turn it clockwise to show it".
        var variants = ImageVariants.Create(TestImages.WithExif(TestImages.Jpeg(400, 200), orientation), MediaPurpose.PersonPhoto).Value;

        var medium = variants.Single(v => v.Variant == MediaVariant.Medium);
        (medium.Width, medium.Height).ShouldBe((width, height));
        TestImages.ColourName(TestImages.PixelAt(medium.Bytes, 10, 10)).ShouldBe(topLeft);
    }

    [Fact]
    public void An_image_over_40_megapixels_is_refused_before_it_is_decoded() =>
        ImageVariants.Create(TestImages.PngClaiming(8000, 6000), MediaPurpose.PersonPhoto).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);

    [Fact]
    public void A_file_with_an_image_header_that_does_not_decode_is_refused()
    {
        var broken = TestImages.Jpeg(64, 64)[..12];

        ImageVariants.Create(broken, MediaPurpose.PersonPhoto).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);
    }

    [Fact]
    public void A_large_PNG_is_shrunk_before_it_is_oriented()
    {
        // 3200×2400: PNG can't decode scaled, so the full bitmap is shrunk to 1600×1200 first.
        var png = ImageVariants.Create(TestImages.Png(3200, 2400), MediaPurpose.PersonPhoto).Value;

        png.Select(v => (v.Width, v.Height)).ShouldBe([(160, 120), (640, 480), (1600, 1200)]);
        TestImages.ColourName(TestImages.PixelAt(png[^1].Bytes, 10, 10)).ShouldBe("red");
        TestImages.ColourName(TestImages.PixelAt(png[^1].Bytes, 1590, 1190)).ShouldBe("yellow");
    }

    [Theory]
    [InlineData(MediaFileType.Jpeg, 6, 300, 400)]
    [InlineData(MediaFileType.Jpeg, 1, 400, 300)]
    public void A_document_JPEG_keeps_its_size_and_type_but_loses_its_EXIF_data(
        MediaFileType type, ushort orientation, int width, int height)
    {
        // A photographed consent form, 400×300 with a GPS position; orientation 6 means it was taken turned.
        var upload = TestImages.WithExif(TestImages.Jpeg(400, 300), orientation);

        var document = ImageVariants.Reencode(upload, type).Value;

        document.Variant.ShouldBe(MediaVariant.Original);
        (document.Width, document.Height).ShouldBe((width, height));
        MediaRules.Detect(document.Bytes).ShouldBe(MediaFileType.Jpeg);
        TestImages.SizeOf(document.Bytes).ShouldBe((width, height));
        document.Bytes.AsSpan().IndexOf("Exif"u8).ShouldBe(-1);
    }

    [Fact]
    public void A_document_PNG_stays_a_PNG_at_its_size()
    {
        var document = ImageVariants.Reencode(TestImages.Png(1200, 1600), MediaFileType.Png).Value;

        MediaRules.Detect(document.Bytes).ShouldBe(MediaFileType.Png);
        TestImages.SizeOf(document.Bytes).ShouldBe((1200, 1600));
        TestImages.ColourName(TestImages.PixelAt(document.Bytes, 1190, 1590)).ShouldBe("yellow");
    }

    [Fact]
    public void A_broken_document_image_is_refused()
    {
        var jpeg = TestImages.Jpeg(400, 300);

        ImageVariants.Reencode(jpeg[..(jpeg.Length / 2)], MediaFileType.Jpeg).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);
        ImageVariants.Reencode(TestImages.PngClaiming(8000, 6000), MediaFileType.Png).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);
    }

    [Fact]
    public void A_truncated_image_is_refused()
    {
        var jpeg = TestImages.Jpeg(800, 600);

        ImageVariants.Create(jpeg[..(jpeg.Length * 2 / 3)], MediaPurpose.PersonPhoto).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);
    }

    [Fact]
    public void A_photo_much_larger_than_the_largest_variant_still_gets_its_full_sizes()
    {
        // 4000×2000: a JPEG decodes at half size (2000×1000), which still covers the 1600 px variant.
        var variants = ImageVariants.Create(TestImages.Jpeg(4000, 2000), MediaPurpose.PersonPhoto).Value;

        variants.Select(v => (v.Width, v.Height)).ShouldBe([(160, 80), (640, 320), (1600, 800)]);
        TestImages.ColourName(TestImages.PixelAt(variants[^1].Bytes, 10, 10)).ShouldBe("red");
        TestImages.ColourName(TestImages.PixelAt(variants[^1].Bytes, 1590, 790)).ShouldBe("yellow");
    }

    [Theory]
    [InlineData(2000, 1000, 160, 160, 80)]
    [InlineData(1000, 2000, 160, 80, 160)]
    [InlineData(100, 50, 160, 100, 50)]
    [InlineData(3, 1000, 160, 1, 160)]
    public void Scales_to_the_longest_side(int width, int height, int side, int expectedWidth, int expectedHeight) =>
        ImageVariants.ScaledSize(width, height, side).ShouldBe((expectedWidth, expectedHeight));

    [Theory]
    [InlineData(MediaPurpose.OrgLogo)]
    [InlineData(MediaPurpose.TeamLogo)]
    [InlineData(MediaPurpose.TeamBadge)]
    public void A_logo_or_badge_gets_a_thumbnail_and_a_medium_only(MediaPurpose purpose)
    {
        var variants = ImageVariants.Create(TestImages.Png(1000, 800), purpose).Value;

        variants.Select(v => (v.Variant, v.Width, v.Height)).ShouldBe(
            [(MediaVariant.Thumbnail, 160, 128), (MediaVariant.Medium, 640, 512)]);
    }

    // T4 acceptance 1: a PNG with transparency keeps it in the WebP variants.
    [Fact]
    public void A_transparent_logo_stays_transparent_in_every_variant()
    {
        var variants = ImageVariants.Create(TestImages.TransparentPng(400, 200), MediaPurpose.TeamBadge).Value;

        foreach (var variant in variants)
        {
            TestImages.PixelAt(variant.Bytes, 2, variant.Height / 2).Alpha.ShouldBe((byte)0);
            var opaque = TestImages.PixelAt(variant.Bytes, variant.Width - 3, variant.Height / 2);
            opaque.Alpha.ShouldBe((byte)255);
            TestImages.ColourName(opaque).ShouldBe("red");
        }
    }

    // T4 acceptance 1: an image under 64 px on its shorter side is refused, whichever side that is.
    [Theory]
    [InlineData(63, 400)]
    [InlineData(400, 63)]
    public void A_logo_under_64_px_on_its_shorter_side_is_too_small(int width, int height) =>
        ImageVariants.Create(TestImages.Png(width, height), MediaPurpose.OrgLogo).Error!.Code
            .ShouldBe(PlatformErrors.MediaImageTooSmall.Code);

    [Fact]
    public void A_logo_of_64_px_is_accepted_and_a_small_photo_has_no_minimum()
    {
        ImageVariants.Create(TestImages.Png(64, 64), MediaPurpose.TeamLogo).IsSuccess.ShouldBeTrue();
        ImageVariants.Create(TestImages.Png(20, 20), MediaPurpose.PersonPhoto).IsSuccess.ShouldBeTrue();
    }

    private static bool ContainsBytes(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
