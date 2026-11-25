using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform;

/// <summary>The Platform module's expected failures; exported to docs/generated/errors.md by the CLI.</summary>
public static class PlatformErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "PLATFORM_CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload and try again.", ErrorKind.Conflict);

    public static readonly Error OutboxMessageNotFound = new(
        "PLATFORM_OUTBOX_MESSAGE_NOT_FOUND", "No outbox message with this ID in this module.", ErrorKind.NotFound);

    public static readonly Error OutboxMessageNotFailed = new(
        "PLATFORM_OUTBOX_MESSAGE_NOT_FAILED", "Only a FAILED outbox message can be re-driven.", ErrorKind.Conflict);

    public static readonly Error MediaTypeNotAllowed = new(
        "MEDIA_TYPE_NOT_ALLOWED", "The file's type isn't accepted here. Photos, logos and badges: JPEG, PNG or WebP. Documents: PDF, JPEG or PNG.", ErrorKind.Unprocessable);

    public static readonly Error MediaTooLarge = new(
        "MEDIA_TOO_LARGE", "The file is too large. Photos: up to 5 MB. Logos and badges: up to 2 MB. Documents: up to 10 MB.", ErrorKind.PayloadTooLarge);

    public static readonly Error MediaImageUnreadable = new(
        "MEDIA_IMAGE_UNREADABLE", "The image can't be read, or it is larger than 40 megapixels.", ErrorKind.Unprocessable);

    public static readonly Error MediaImageTooSmall = new(
        "MEDIA_IMAGE_TOO_SMALL", "The image is too small. Logos and badges: at least 64 px on the shorter side.", ErrorKind.Unprocessable);

    public static readonly Error MediaNotFound = new(
        "MEDIA_NOT_FOUND", "The file doesn't exist, was removed, or its link has expired.", ErrorKind.NotFound);
}
