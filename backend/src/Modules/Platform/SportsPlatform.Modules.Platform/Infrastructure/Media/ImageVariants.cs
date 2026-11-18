using SkiaSharp;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Features.Media;

namespace SportsPlatform.Modules.Platform.Infrastructure.Media;

/// <summary>One encoded variant of a photo.</summary>
public sealed record EncodedImage(MediaVariant Variant, byte[] Bytes, int Width, int Height);

/// <summary>
/// A photo's variants (ADR-0017): the header is read first and an image over <see cref="MaxPixels"/> is refused before
/// it is decoded; the EXIF orientation is applied; each size in <see cref="MediaRules.ImageVariantSizes"/> is scaled
/// down (never up) and encoded as WebP. Encoding writes pixels only, so no EXIF, GPS or XMP data reaches a variant.
/// </summary>
public static class ImageVariants
{
    /// <summary>40 megapixels: a larger image is refused (a small file can't expand into a huge bitmap).</summary>
    public const long MaxPixels = 40_000_000;

    public const int WebPQuality = 80;

    public static Result<IReadOnlyList<EncodedImage>> Create(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var data = SKData.CreateCopy(file);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0
            || (long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return PlatformErrors.MediaImageUnreadable;
        }

        var info = codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul);
        using var decoded = new SKBitmap(info);
        var decodeResult = codec.GetPixels(info, decoded.GetPixels());
        if (decodeResult is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            return PlatformErrors.MediaImageUnreadable;
        }

        using var oriented = Orient(decoded, codec.EncodedOrigin);
        var variants = new List<EncodedImage>();
        foreach (var (variant, longestSide) in MediaRules.ImageVariantSizes)
        {
            var (width, height) = ScaledSize(oriented.Width, oriented.Height, longestSide);
            using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!oriented.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell)))
            {
                return PlatformErrors.MediaImageUnreadable;
            }

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebPQuality);
            if (encoded is null)
            {
                throw new InvalidOperationException("SkiaSharp could not encode a WebP variant.");
            }

            variants.Add(new EncodedImage(variant, encoded.ToArray(), width, height));
        }

        return Result.Success<IReadOnlyList<EncodedImage>>(variants);
    }

    /// <summary>The size that fits <paramref name="longestSide"/>, keeping the aspect ratio; never larger than the image.</summary>
    public static (int Width, int Height) ScaledSize(int width, int height, int longestSide)
    {
        var scale = Math.Min(1d, (double)longestSide / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>The pixels as they should be shown: the EXIF orientation applied (a copy in every case).</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width;
        float h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // Each matrix maps a source point (x, y) to where it is shown: x' = a·x + b·y + c, y' = d·x + e·y + f.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var result = new SKBitmap(new SKImageInfo(
            swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(matrix);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        canvas.Flush();
        return result;
    }
}
