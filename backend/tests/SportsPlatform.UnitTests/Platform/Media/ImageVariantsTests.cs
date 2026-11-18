using System.Text;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Infrastructure.Media;

namespace SportsPlatform.UnitTests.Platform.Media;

public sealed class ImageVariantsTests
{
    [Fact]
    public void A_large_photo_gets_three_WebP_variants_by_its_longest_side()
    {
        var variants = ImageVariants.Create(TestImages.Jpeg(2000, 1000)).Value;

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
        var variants = ImageVariants.Create(TestImages.Png(300, 400)).Value;

        variants.Select(v => (v.Width, v.Height)).ShouldBe([(120, 160), (300, 400), (300, 400)]);
    }

    [Fact]
    public void Variants_carry_no_EXIF_data_such_as_a_GPS_position()
    {
        var upload = TestImages.WithExif(TestImages.Jpeg(800, 600), orientation: 1);
        ContainsBytes(upload, "Exif\0\0"u8.ToArray()).ShouldBeTrue();

        var variants = ImageVariants.Create(upload).Value;

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
        var variants = ImageVariants.Create(TestImages.WithExif(TestImages.Jpeg(400, 200), orientation)).Value;

        var medium = variants.Single(v => v.Variant == MediaVariant.Medium);
        (medium.Width, medium.Height).ShouldBe((width, height));
        TestImages.ColourName(TestImages.PixelAt(medium.Bytes, 10, 10)).ShouldBe(topLeft);
    }

    [Fact]
    public void An_image_over_40_megapixels_is_refused_before_it_is_decoded() =>
        ImageVariants.Create(TestImages.PngClaiming(8000, 6000)).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);

    [Fact]
    public void A_file_with_an_image_header_that_does_not_decode_is_refused()
    {
        var broken = TestImages.Jpeg(64, 64)[..12];

        ImageVariants.Create(broken).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);
    }

    [Fact]
    public void A_large_PNG_is_shrunk_before_it_is_oriented()
    {
        // 3200×2400: PNG can't decode scaled, so the full bitmap is shrunk to 1600×1200 first.
        var png = ImageVariants.Create(TestImages.Png(3200, 2400)).Value;

        png.Select(v => (v.Width, v.Height)).ShouldBe([(160, 120), (640, 480), (1600, 1200)]);
        TestImages.ColourName(TestImages.PixelAt(png[^1].Bytes, 10, 10)).ShouldBe("red");
        TestImages.ColourName(TestImages.PixelAt(png[^1].Bytes, 1590, 1190)).ShouldBe("yellow");
    }

    [Fact]
    public void A_truncated_image_is_refused()
    {
        var jpeg = TestImages.Jpeg(800, 600);

        ImageVariants.Create(jpeg[..(jpeg.Length * 2 / 3)]).Error!.Code.ShouldBe(PlatformErrors.MediaImageUnreadable.Code);
    }

    [Fact]
    public void A_photo_much_larger_than_the_largest_variant_still_gets_its_full_sizes()
    {
        // 4000×2000: a JPEG decodes at half size (2000×1000), which still covers the 1600 px variant.
        var variants = ImageVariants.Create(TestImages.Jpeg(4000, 2000)).Value;

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

    private static bool ContainsBytes(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
