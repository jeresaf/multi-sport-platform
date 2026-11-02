using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.SportsConfiguration.Domain.Definitions;

namespace SportsPlatform.Modules.SportsConfiguration.Domain.Rulesets;

/// <summary>
/// A ruleset template as stored (<c>config.ruleset_templates</c>, S05 D4, D11): one row per code and version, global,
/// read-only for the runtime roles and written only by <c>config.configuration.rulesets</c>. Immutable: a row is never
/// updated or deleted; a changed template is a new version. The sport variant and version and the formats are columns as
/// well as content, so a later slice can find the templates of a variant without reading every row's content.
/// </summary>
public sealed class PublishedRulesetTemplate : Entity, IPublishedDefinition
{
    public PublishedRulesetTemplate(
        string code,
        int version,
        string name,
        string sportVariant,
        int sportVersion,
        IEnumerable<string> formats,
        string content,
        string contentHash,
        DateTimeOffset publishedAt)
        : base(IdFor(code, version))
    {
        Code = code;
        Version = version;
        Name = name;
        SportVariant = sportVariant;
        SportVersion = sportVersion;
        Formats = [.. formats];
        Status = DefinitionStatus.Published;
        Content = content;
        ContentHash = contentHash;
        PublishedAt = publishedAt;
    }

    private PublishedRulesetTemplate()
    {
    }

    /// <summary>e.g. <c>FOOTBALL_LEAGUE</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The sport definition's variant the template is written for.</summary>
    public string SportVariant { get; private set; } = string.Empty;

    /// <summary>The sport definition's version the template is written for.</summary>
    public int SportVersion { get; private set; }

    /// <summary>The codes of the formats the template works with.</summary>
    public List<string> Formats { get; private set; } = [];

    public DefinitionStatus Status { get; private set; }

    /// <summary>
    /// The file's content, written as its RFC 8785 canonical text into a <c>jsonb</c> column (which keeps the tree, not
    /// the text: canonicalize it again before hashing).
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary><c>sha256:</c> and the hex SHA-256 of <see cref="Content"/>.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; private set; }

    public static Guid IdFor(string code, int version) => DeterministicIds.Create($"config.ruleset_template:{code}@{version}");
}
