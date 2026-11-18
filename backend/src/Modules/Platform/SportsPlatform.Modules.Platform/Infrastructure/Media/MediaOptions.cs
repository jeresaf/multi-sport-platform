namespace SportsPlatform.Modules.Platform.Infrastructure.Media;

/// <summary>
/// Media storage (<c>Media:*</c>, S06 D16). The local-disk adapter writes under <see cref="LocalRoot"/>: a relative path
/// is taken from the working directory (the repository root for the commands in CLAUDE.md, so the API and the CLI share
/// <c>.media/</c>, git-ignored, in Development); tests use a temporary folder. No default: an environment without it can
/// run, but storing or reading a file fails until it is set.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string? LocalRoot { get; set; }
}
