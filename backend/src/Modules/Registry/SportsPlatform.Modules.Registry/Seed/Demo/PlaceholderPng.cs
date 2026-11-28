using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SportsPlatform.Modules.Registry.Seed.Demo;

/// <summary>
/// Generated placeholder images for the demo people (S06 D23) and the organisations' logos and teams' badges (S07 D22):
/// never a photo of anyone or a real club's emblem. PNG written by hand (no image library: SkiaSharp is Platform media's
/// alone), as e2e's <c>placeholderPng</c>. Platform stores them as any upload.
/// </summary>
internal static class PlaceholderPng
{
    // Disc and background pairs, picked by the caller's index.
    private static readonly (byte[] Disc, byte[] Background)[] _palettes =
    [
        ([64, 120, 170], [214, 226, 236]),
        ([170, 96, 64], [240, 224, 214]),
        ([72, 140, 96], [218, 236, 222]),
        ([128, 88, 160], [230, 220, 240]),
        ([176, 140, 48], [244, 236, 210]),
        ([60, 132, 140], [212, 234, 236]),
    ];

    /// <summary>A <paramref name="size"/> × <paramref name="size"/> disc on a plain background, for a photo.</summary>
    public static byte[] Photo(int size, int palette)
    {
        var (disc, background) = _palettes[Math.Abs(palette % _palettes.Length)];
        var centre = size / 2.0;
        var radius = size * 0.35;
        return Write(size, size, (x, y) => (x - centre) * (x - centre) + (y - centre) * (y - centre) < radius * radius ? disc : background);
    }

    /// <summary>
    /// A <paramref name="size"/> × <paramref name="size"/> shield with a stripe on a plain background, standing in for
    /// an organisation's logo or a team's badge (S07 D22): never a real club's.
    /// </summary>
    public static byte[] Logo(int size, int palette)
    {
        var (shield, background) = _palettes[Math.Abs(palette % _palettes.Length)];
        byte[] stripe = [250, 250, 250];
        double left = size * 0.2, right = size * 0.8, top = size * 0.15, shoulder = size * 0.55, tip = size * 0.88;
        var centre = size / 2.0;
        return Write(size, size, (x, y) =>
        {
            // A rectangle down to the shoulder, then narrowing to the tip.
            var halfWidth = y < shoulder ? (right - left) / 2 : (right - left) / 2 * (tip - y) / (tip - shoulder);
            var inside = y >= top && y < tip && Math.Abs(x - centre) < halfWidth;
            return !inside ? background : Math.Abs(y - (size * 0.42)) < size * 0.06 ? stripe : shield;
        });
    }

    /// <summary>A page with grey ruled lines and a title bar, standing in for a scanned consent form.</summary>
    public static byte[] Form()
    {
        byte[] paper = [250, 250, 246], line = [190, 190, 190], title = [90, 90, 90];
        return Write(420, 594, (x, y) => (x, y) switch
        {
            _ when x < 30 || x >= 390 => paper,
            _ when y is >= 40 and < 60 && x < 260 => title,
            _ when y >= 100 && y < 560 && y % 36 == 0 => line,
            _ => paper,
        });
    }

    private static byte[] Write(int width, int height, Func<int, int, byte[]> pixel)
    {
        var raw = new byte[height * (1 + (width * 3))];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + (width * 3)); // Filter byte 0 (none) starts each row.
            for (var x = 0; x < width; x++)
            {
                pixel(x, y).CopyTo(raw, row + 1 + (x * 3));
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8; // 8 bits per channel, truecolour (2), default compression, filter and no interlace.
        header[9] = 2;

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        png.Write(number);
        var body = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body, 4);
        png.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(body));
        png.Write(number);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
