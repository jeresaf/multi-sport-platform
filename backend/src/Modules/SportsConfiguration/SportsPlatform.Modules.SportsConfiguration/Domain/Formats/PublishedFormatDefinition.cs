using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.SportsConfiguration.Domain.Definitions;

namespace SportsPlatform.Modules.SportsConfiguration.Domain.Formats;

/// <summary>
/// A format definition as stored (<c>config.format_definitions</c>, S05 D4, D10): one row per code and version, global,
/// read-only for the runtime roles and written only by <c>config.configuration.formats</c>. Immutable: a row is never
/// updated or deleted; a changed format is a new version.
/// </summary>
public sealed class PublishedFormatDefinition : Entity, IPublishedDefinition
{
    public PublishedFormatDefinition(string code, int version, string name, string content, string contentHash, DateTimeOffset publishedAt)
        : base(IdFor(code, version))
    {
        Code = code;
        Version = version;
        Name = name;
        Status = DefinitionStatus.Published;
        Content = content;
        ContentHash = contentHash;
        PublishedAt = publishedAt;
    }

    private PublishedFormatDefinition()
    {
    }

    /// <summary>e.g. <c>ROUND_ROBIN</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DefinitionStatus Status { get; private set; }

    /// <summary>
    /// The file's content, written as its RFC 8785 canonical text into a <c>jsonb</c> column (which keeps the tree, not
    /// the text: canonicalize it again before hashing).
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary><c>sha256:</c> and the hex SHA-256 of <see cref="Content"/>.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; private set; }

    public static Guid IdFor(string code, int version) => DeterministicIds.Create($"config.format_definition:{code}@{version}");
}
