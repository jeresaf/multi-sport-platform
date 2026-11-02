using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// A published ruleset template, resolved (docs/domain/rulesets.md, schema version 1): a sport variant at an exact
/// version, the formats it works with, its default <see cref="Values"/> and the parameters tenants may change.
/// <see cref="Editable"/> is keyed by the parameter's dotted path into the values (<c>standings.points.win</c>); each
/// parameter's default is the value at its path.
/// </summary>
public sealed record RulesetTemplate(
    int SchemaVersion,
    string Code,
    int Version,
    string Name,
    RulesetSportReference Sport,
    IReadOnlyList<string> Formats,
    IReadOnlyDictionary<string, ParameterDefinition> Editable,
    RulesetValues Values)
{
    public string? Description { get; init; }

    /// <summary><c>sha256:</c> and the hex SHA-256 of the content in RFC 8785 canonical JSON (S05 D4).</summary>
    [JsonIgnore]
    public string ContentHash { get; init; } = string.Empty;

    [JsonIgnore]
    public DateTimeOffset PublishedAt { get; init; }
}

/// <summary>The sport definition a template is written for, at the version its period and position codes come from.</summary>
public sealed record RulesetSportReference(string Variant, int Version);
