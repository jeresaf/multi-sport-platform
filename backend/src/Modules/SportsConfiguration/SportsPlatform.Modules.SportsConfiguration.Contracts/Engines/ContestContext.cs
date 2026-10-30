using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// What an engine knows about a contest besides its timeline: the resolved definition version, the effective rules,
/// the sides, the coverage level and the lineups (sport-engine.md §4). Ids are the contest's participant and athlete
/// ids; Sports Configuration never looks them up.
/// </summary>
public sealed record ContestContext(
    SportDefinition Definition,
    ContestRules Rules,
    IReadOnlyList<ContestSide> Sides,
    CoverageLevel Coverage)
{
    /// <summary>The approved lineups, one per participant; empty below <c>STANDARD</c> coverage.</summary>
    public IReadOnlyList<ContestLineup> Lineups { get; init; } = [];

    /// <summary>The participant on the other side of a head-to-head contest.</summary>
    public Guid OpponentOf(Guid participantId) =>
        Sides.Count == 2 && Sides.Any(side => side.ParticipantId == participantId)
            ? Sides.Single(side => side.ParticipantId != participantId).ParticipantId
            : throw new InvalidOperationException($"Participant {participantId} has no single opponent in this contest.");
}

/// <summary>A participant and the definition's side it plays on (<c>HOME</c>).</summary>
public sealed record ContestSide(string Side, Guid ParticipantId);

/// <summary>A participant's starters, bench and staff at the start of the contest.</summary>
public sealed record ContestLineup(Guid ParticipantId, IReadOnlyList<Guid> Starters, IReadOnlyList<Guid> Bench)
{
    public IReadOnlyList<Guid> Staff { get; init; } = [];
}
