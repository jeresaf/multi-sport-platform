using SkiaSharp;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Features.Media;

namespace SportsPlatform.Modules.Platform.Infrastructure.Media;

/// <summary>One encoded image: a photo's variant or a re-encoded document image.</summary>
public sealed record EncodedImage(MediaVariant Variant, byte[] Bytes, int Width, int Height);

/// <summary>
/// An image's variants (ADR-0017): the header is read first and an image over <see cref="MaxPixels"/>, or (a logo or
/// badge) under the purpose's <see cref="MediaRules.MinShorterSide"/>, is refused before it is decoded; a file that
/// doesn't decode completely (truncated) is refused; the EXIF orientation is applied; each size in
/// <see cref="MediaRules.VariantSizes"/> is scaled down (never up) and encoded as WebP with its alpha channel, so a
/// transparent logo stays transparent. Encoding writes pixels only, so no EXIF, GPS or XMP data reaches a variant.
/// A document image (consent evidence, JPEG or PNG) is re-encoded once, at its own size and type, for the same reason
/// (review #104 Q1, owner).
/// </summary>
public static class ImageVariants
{
    /// <summary>40 megapixels: a larger image is refused (a small file can't expand into a huge bitmap).</summary>
    public const long MaxPixels = 40_000_000;

    public const int WebPQuality = 80;

    /// <summary>A re-encoded document JPEG's quality: high, since a form must stay legible.</summary>
    public const int DocumentJpegQuality = 92;

    public static Result<IReadOnlyList<EncodedImage>> Create(byte[] file, MediaPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(file);
        var sizes = MediaRules.VariantSizes(purpose);
        using var data = SKData.CreateCopy(file);
        using var codec = OpenCodec(data);
        if (codec is not null && Math.Min(codec.Info.Width, codec.Info.Height) < MediaRules.MinShorterSide(purpose))
        {
            return PlatformErrors.MediaImageTooSmall;
        }

        using var oriented = codec is null ? null : DecodeOriented(codec, sizes.Max(size => size.LongestSide));
        if (oriented is null)
        {
            return PlatformErrors.MediaImageUnreadable;
        }

        var variants = new List<EncodedImage>();
        foreach (var (variant, longestSide) in sizes)
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

    /// <summary>
    /// A document image (JPEG or PNG) as the <see cref="MediaVariant.Original"/>: oriented and re-encoded at its own size
    /// and in its own type (PNG lossless), so its metadata (EXIF, GPS, XMP) is dropped.
    /// </summary>
    public static Result<EncodedImage> Reencode(byte[] file, MediaFileType type)
    {
        ArgumentNullException.ThrowIfNull(file);
        var (format, quality) = type switch
        {
            MediaFileType.Jpeg => (SKEncodedImageFormat.Jpeg, DocumentJpegQuality),
            MediaFileType.Png => (SKEncodedImageFormat.Png, 100),
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Only JPEG and PNG documents are re-encoded."),
        };

        using var data = SKData.CreateCopy(file);
        using var codec = OpenCodec(data);
        using var oriented = codec is null ? null : DecodeOriented(codec, maxLongestSide: null);
        if (oriented is null)
        {
            return PlatformErrors.MediaImageUnreadable;
        }

        using var image = SKImage.FromBitmap(oriented);
        using var encoded = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"SkiaSharp could not encode a {type} document.");
        return Result.Success(new EncodedImage(MediaVariant.Original, encoded.ToArray(), oriented.Width, oriented.Height));
    }

    /// <summary>The size that fits <paramref name="longestSide"/>, keeping the aspect ratio; never larger than the image.</summary>
    public static (int Width, int Height) ScaledSize(int width, int height, int longestSide)
    {
        var scale = Math.Min(1d, (double)longestSide / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>The image's codec, if its header is readable and it has at most <see cref="MaxPixels"/>.</summary>
    private static SKCodec? OpenCodec(SKData data)
    {
        var codec = SKCodec.Create(data);
        if (codec is not null && codec.Info.Width > 0 && codec.Info.Height > 0
            && (long)codec.Info.Width * codec.Info.Height <= MaxPixels)
        {
            return codec;
        }

        codec?.Dispose();
        return null;
    }

    /// <summary>
    /// The pixels, oriented, at most <paramref name="maxLongestSide"/> on the longest side (null: full size). JPEG decodes
    /// at 1/2, 1/4 or 1/8 directly when that still covers the size; anything larger is shrunk before it is oriented, so a
    /// large photo needs its full bitmap once at most, and an image the right way up isn't copied. A truncated or broken
    /// file gives null.
    /// </summary>
    private static SKBitmap? DecodeOriented(SKCodec codec, int? maxLongestSide)
    {
        var full = codec.Info.Size;
        var largest = maxLongestSide ?? Math.Max(full.Width, full.Height);
        var scale = Math.Min(1f, (float)largest / Math.Max(full.Width, full.Height));
        var scaled = codec.GetScaledDimensions(scale);
        if (Math.Max(scaled.Width, scaled.Height) < Math.Min(largest, Math.Max(full.Width, full.Height)))
        {
            scaled = full;
        }

        var bitmap = Decode(codec, scaled) ?? (scaled == full ? null : Decode(codec, full));
        if (bitmap is null)
        {
            return null;
        }

        // Shrink to the largest variant first (PNG and WebP can't decode scaled), so orienting copies a small bitmap.
        var (width, height) = ScaledSize(bitmap.Width, bitmap.Height, largest);
        if (width != bitmap.Width || height != bitmap.Height)
        {
            using var large = bitmap;
            bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!large.ScalePixels(bitmap, new SKSamplingOptions(SKCubicResampler.Mitchell)))
            {
                bitmap.Dispose();
                return null;
            }

            bitmap.SetImmutable();
        }

        if (codec.EncodedOrigin is SKEncodedOrigin.TopLeft)
        {
            return bitmap;
        }

        using (bitmap)
        {
            return Orient(bitmap, codec.EncodedOrigin);
        }
    }

    private static SKBitmap? Decode(SKCodec codec, SKSizeI size)
    {
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) == SKCodecResult.Success)
        {
            bitmap.SetImmutable();
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    /// <summary>The pixels as they should be shown: the EXIF orientation applied (a copy).</summary>
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
