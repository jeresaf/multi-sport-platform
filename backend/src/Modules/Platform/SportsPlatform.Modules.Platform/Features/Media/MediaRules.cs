using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;

namespace SportsPlatform.Modules.Platform.Features.Media;

/// <summary>A file type Platform recognises by its first bytes.</summary>
public enum MediaFileType
{
    Jpeg,
    Png,
    WebP,
    Pdf,
}

/// <summary>
/// What each purpose accepts (S06 D16, S07 D15): the types, judged by the file's first bytes and never by the client's content
/// type or file name, the size limit, the variants and who may get it. A photo, logo or badge is re-encoded into
/// variants; a PDF is stored as uploaded and a document image re-encoded once, without its metadata.
/// </summary>
public static class MediaRules
{
    public const long Megabyte = 1024 * 1024;

    public static long MaxBytes(MediaPurpose purpose) => purpose switch
    {
        MediaPurpose.PersonPhoto => 5 * Megabyte,
        MediaPurpose.ConsentEvidence => 10 * Megabyte,
        MediaPurpose.OrgLogo or MediaPurpose.TeamLogo or MediaPurpose.TeamBadge => 2 * Megabyte,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };

    public static IReadOnlySet<MediaFileType> AllowedTypes(MediaPurpose purpose) => purpose switch
    {
        MediaPurpose.PersonPhoto or MediaPurpose.OrgLogo or MediaPurpose.TeamLogo or MediaPurpose.TeamBadge =>
            new HashSet<MediaFileType> { MediaFileType.Jpeg, MediaFileType.Png, MediaFileType.WebP },
        MediaPurpose.ConsentEvidence => new HashSet<MediaFileType> { MediaFileType.Pdf, MediaFileType.Jpeg, MediaFileType.Png },
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };

    /// <summary>Whether the purpose's files are images re-encoded into <see cref="VariantSizes"/>.</summary>
    public static bool MakesVariants(MediaPurpose purpose) => purpose != MediaPurpose.ConsentEvidence;

    /// <summary>
    /// The variants of an image purpose and the longest side of each, in pixels (ADR-0017): a photo's thumbnail, medium
    /// and large; a logo's or badge's thumbnail and medium (S07 D15).
    /// </summary>
    public static IReadOnlyList<(MediaVariant Variant, int LongestSide)> VariantSizes(MediaPurpose purpose) => purpose switch
    {
        MediaPurpose.PersonPhoto => _photoSizes,
        MediaPurpose.OrgLogo or MediaPurpose.TeamLogo or MediaPurpose.TeamBadge => _logoSizes,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "The purpose has no image variants."),
    };

    /// <summary>
    /// The shortest side, in pixels, an image of the purpose must have (<c>MEDIA_IMAGE_TOO_SMALL</c> below it): 64 for a
    /// logo or badge (S07 D15), none (1) for a photo.
    /// </summary>
    public static int MinShorterSide(MediaPurpose purpose) => purpose switch
    {
        MediaPurpose.OrgLogo or MediaPurpose.TeamLogo or MediaPurpose.TeamBadge => 64,
        _ => 1,
    };

    /// <summary>
    /// Who may get the purpose's assets: a logo or badge is <c>PUBLIC</c> (S07 D15, the public asset route); a person's
    /// photo and a document are <c>PRIVATE</c> (signed URLs only).
    /// </summary>
    public static MediaVisibility Visibility(MediaPurpose purpose) => purpose switch
    {
        MediaPurpose.OrgLogo or MediaPurpose.TeamLogo or MediaPurpose.TeamBadge => MediaVisibility.Public,
        _ => MediaVisibility.Private,
    };

    private static readonly IReadOnlyList<(MediaVariant, int)> _photoSizes =
    [
        (MediaVariant.Thumbnail, 160),
        (MediaVariant.Medium, 640),
        (MediaVariant.Large, 1600),
    ];

    private static readonly IReadOnlyList<(MediaVariant, int)> _logoSizes =
    [
        (MediaVariant.Thumbnail, 160),
        (MediaVariant.Medium, 640),
    ];

    public static string ContentType(MediaFileType type) => type switch
    {
        MediaFileType.Jpeg => "image/jpeg",
        MediaFileType.Png => "image/png",
        MediaFileType.WebP => "image/webp",
        MediaFileType.Pdf => "application/pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string Extension(MediaFileType type) => type switch
    {
        MediaFileType.Jpeg => "jpg",
        MediaFileType.Png => "png",
        MediaFileType.WebP => "webp",
        MediaFileType.Pdf => "pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>The file extension of a stored content type.</summary>
    public static string Extension(string contentType) =>
        Enum.GetValues<MediaFileType>().Where(type => ContentType(type) == contentType).Select(Extension).FirstOrDefault()
        ?? throw new ArgumentOutOfRangeException(nameof(contentType), "Not a stored media type.");

    /// <summary>The type the file's first bytes show, if Platform knows it.</summary>
    public static MediaFileType? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return MediaFileType.Jpeg;
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return MediaFileType.Png;
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return MediaFileType.WebP;
        }

        return bytes.StartsWith("%PDF-"u8) ? MediaFileType.Pdf : null;
    }

    /// <summary>The file's type if <paramref name="purpose"/> accepts it, else <c>MEDIA_TYPE_NOT_ALLOWED</c>.</summary>
    public static Result<MediaFileType> CheckType(ReadOnlySpan<byte> bytes, MediaPurpose purpose) =>
        Detect(bytes) is { } type && AllowedTypes(purpose).Contains(type)
            ? Result.Success(type)
            : PlatformErrors.MediaTypeNotAllowed;

    /// <summary>
    /// Reads <paramref name="content"/> into memory up to the purpose's limit: one byte more is <c>MEDIA_TOO_LARGE</c>,
    /// so an oversized upload is never read to its end.
    /// </summary>
    public static async Task<Result<byte[]>> ReadBoundedAsync(Stream content, MediaPurpose purpose, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var limit = MaxBytes(purpose);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return PlatformErrors.MediaTooLarge;
            }

            buffer.Write(chunk, 0, read);
        }

        return Result.Success(buffer.ToArray());
    }
}
