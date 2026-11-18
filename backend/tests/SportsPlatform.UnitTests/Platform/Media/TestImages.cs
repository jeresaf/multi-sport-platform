using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace SportsPlatform.UnitTests.Platform.Media;

/// <summary>
/// Generated test files (no photos of anyone): images whose four quadrants are red, green, blue and yellow (top left,
/// top right, bottom left, bottom right), so orientation is visible; a JPEG with an EXIF orientation and a GPS
/// position; a PNG whose header claims a huge size; a small PDF. Also linked into the integration tests.
/// </summary>
public static class TestImages
{
    /// <summary>The GPS latitude written into <see cref="WithExif"/>: 0° 20' 30.00" N (near Kampala).</summary>
    public static readonly byte[] GpsLatitudeRef = "N\0"u8.ToArray();

    public static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    public static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] WebP(int width, int height) => Encode(width, height, SKEncodedImageFormat.Webp);

    public static byte[] Pdf() => Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [] /Count 0 >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

    /// <summary>
    /// <paramref name="jpeg"/> with an EXIF segment (APP1, right after the start marker) holding an orientation and a
    /// GPS latitude, as a phone camera writes them.
    /// </summary>
    public static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        var tiff = new byte[92];
        var span = tiff.AsSpan();
        "II"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 8);

        // IFD0 at 8: Orientation (SHORT) and the GPS IFD pointer (LONG) → 38.
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], 2);
        Entry(span[10..], 0x0112, 3, 1, orientation);
        Entry(span[22..], 0x8825, 4, 1, 38);
        BinaryPrimitives.WriteUInt32LittleEndian(span[34..], 0);

        // GPS IFD at 38: GPSLatitudeRef "N" (ASCII, inline) and GPSLatitude (3 RATIONALs at 68).
        BinaryPrimitives.WriteUInt16LittleEndian(span[38..], 2);
        Entry(span[40..], 0x0001, 2, 2, 0);
        GpsLatitudeRef.CopyTo(span[48..]);
        Entry(span[52..], 0x0002, 5, 3, 68);
        BinaryPrimitives.WriteUInt32LittleEndian(span[64..], 0);
        uint[] rationals = [0, 1, 20, 1, 3000, 100];
        for (var i = 0; i < rationals.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[(68 + (4 * i))..], rationals[i]);
        }

        var segment = new List<byte> { 0xFF, 0xE1 };
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(2 + 6 + tiff.Length));
        segment.AddRange(length);
        segment.AddRange("Exif\0\0"u8.ToArray());
        segment.AddRange(tiff);
        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    /// <summary>A real small PNG whose header (IHDR, with a valid checksum) claims <paramref name="width"/>×<paramref name="height"/>.</summary>
    public static byte[] PngClaiming(int width, int height)
    {
        var png = Png(8, 8);

        // Signature (8), IHDR length (4), "IHDR" (4), width (4), height (4), …, CRC over type and data (17 bytes).
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), height);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29), Crc32(png.AsSpan(12, 17)));
        return png;
    }

    /// <summary>The FourCC of every chunk of a WebP file (RIFF), e.g. <c>VP8 </c>, <c>EXIF</c>, <c>XMP </c>.</summary>
    public static IReadOnlyList<string> WebPChunks(byte[] webp)
    {
        var chunks = new List<string>();
        var offset = 12;
        while (offset + 8 <= webp.Length)
        {
            chunks.Add(Encoding.ASCII.GetString(webp, offset, 4));
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(offset + 4));
            offset += 8 + size + (size % 2);
        }

        return chunks;
    }

    /// <summary>The colour of a pixel of an encoded image.</summary>
    public static SKColor PixelAt(byte[] encoded, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(encoded);
        return bitmap.GetPixel(x, y);
    }

    public static (int Width, int Height) SizeOf(byte[] encoded)
    {
        using var codec = SKCodec.Create(SKData.CreateCopy(encoded));
        return (codec.Info.Width, codec.Info.Height);
    }

    /// <summary>Which quadrant colour a pixel is closest to.</summary>
    public static string ColourName(SKColor colour) =>
        new[] { ("red", SKColors.Red), ("green", SKColors.Lime), ("blue", SKColors.Blue), ("yellow", SKColors.Yellow) }
            .MinBy(candidate => Math.Abs(candidate.Item2.Red - colour.Red) + Math.Abs(candidate.Item2.Green - colour.Green)
                + Math.Abs(candidate.Item2.Blue - colour.Blue))
            .Item1;

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            float halfW = width / 2f;
            float halfH = height / 2f;
            using var paint = new SKPaint();
            (SKColor Colour, SKRect Rect)[] quadrants =
            [
                (SKColors.Red, new SKRect(0, 0, halfW, halfH)),
                (SKColors.Lime, new SKRect(halfW, 0, width, halfH)),
                (SKColors.Blue, new SKRect(0, halfH, halfW, height)),
                (SKColors.Yellow, new SKRect(halfW, halfH, width, height)),
            ];
            foreach (var (colour, rect) in quadrants)
            {
                paint.Color = colour;
                canvas.DrawRect(rect, paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static void Entry(Span<byte> span, ushort tag, ushort type, uint count, uint value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, tag);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], type);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], count);
        if (type == 3)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[8..], (ushort)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[8..], value);
        }
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
