using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Domain.Sports;

/// <summary>
/// A sport definition as stored (<c>config.sport_definitions</c>, S05 D4): one row per variant and version, global,
/// read-only for the runtime roles and written only by <c>config.configuration.sports</c>. Immutable: a row is never
/// updated or deleted; a changed definition is a new version.
/// </summary>
public sealed class PublishedSportDefinition : Entity
{
    public PublishedSportDefinition(
        string variant,
        string sport,
        int version,
        string name,
        string scoringFamily,
        ContestShape shape,
        string content,
        string contentHash,
        DateTimeOffset publishedAt)
        : base(IdFor(variant, version))
    {
        Variant = variant;
        Sport = sport;
        Version = version;
        Name = name;
        ScoringFamily = scoringFamily;
        Shape = shape;
        Status = DefinitionStatus.Published;
        Content = content;
        ContentHash = contentHash;
        PublishedAt = publishedAt;
    }

    private PublishedSportDefinition()
    {
    }

    /// <summary>e.g. <c>FOOTBALL_ASSOCIATION</c>.</summary>
    public string Variant { get; private set; } = string.Empty;

    /// <summary>What the sport quota counts, e.g. <c>RUGBY_UNION</c> for both rugby variants.</summary>
    public string Sport { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ScoringFamily { get; private set; } = string.Empty;

    public ContestShape Shape { get; private set; }

    public DefinitionStatus Status { get; private set; }

    /// <summary>
    /// The file's content, written as its RFC 8785 canonical text into a <c>jsonb</c> column. <c>jsonb</c> keeps the tree,
    /// not the text (it reads back in its own key order), so canonicalize it again before hashing.
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary><c>sha256:</c> and the hex SHA-256 of <see cref="Content"/>.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; private set; }

    public static Guid IdFor(string variant, int version) => DeterministicIds.Create($"config.sport_definition:{variant}@{version}");
}

/// <summary>Only published definitions exist until the in-app editors (ADR-0006).</summary>
public enum DefinitionStatus
{
    Published,
}
