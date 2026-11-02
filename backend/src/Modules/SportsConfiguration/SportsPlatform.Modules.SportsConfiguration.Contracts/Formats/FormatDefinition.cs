using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;

/// <summary>
/// A published format definition, resolved (docs/domain/formats.md, schema version 1): how a competition is organised,
/// never how a sport scores. Members follow the file's keys; <see cref="Parameters"/> is keyed by the parameter's code
/// (<c>cycles</c>).
/// </summary>
public sealed record FormatDefinition(
    int SchemaVersion,
    string Code,
    int Version,
    string Name,
    IReadOnlyList<ContestShape> CompatibleShapes,
    ParticipantRange Participants,
    IReadOnlyList<FormatStage> Stages,
    IReadOnlyDictionary<string, ParameterDefinition> Parameters)
{
    public string? Description { get; init; }

    /// <summary><c>sha256:</c> and the hex SHA-256 of the content in RFC 8785 canonical JSON (S05 D4).</summary>
    [JsonIgnore]
    public string ContentHash { get; init; } = string.Empty;

    [JsonIgnore]
    public DateTimeOffset PublishedAt { get; init; }
}

/// <summary>How many participants the format takes, both ends included.</summary>
public sealed record ParticipantRange(int Min, int Max);

/// <summary>A stage and the generator that builds its rounds and pairings (<see cref="FormatGeneratorKinds"/>).</summary>
public sealed record FormatStage(string Code, StageType Type, string Generator);

/// <summary>The stage types of formats.md; <c>PLACEMENT</c>, <c>LEADERBOARD</c>, <c>HEATS</c> and <c>CARD</c> come later.</summary>
public enum StageType
{
    League,
    Group,
    Knockout,
    Series,

    /// <summary>One contest (<c>SINGLE</c>).</summary>
    [SuppressMessage("Naming", "CA1720", Justification = "The file's value SINGLE (formats.md), not a type name.")]
    Single,
}
