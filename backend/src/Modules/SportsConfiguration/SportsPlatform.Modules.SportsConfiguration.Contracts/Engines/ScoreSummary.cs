namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// The family-neutral score every client renders (sport-engine.md §4, §10): text only, so renderers never compute.
/// Participants are in the order of the context's sides.
/// </summary>
public sealed record ScoreSummary(IReadOnlyList<ParticipantScore> Participants);

/// <summary>
/// One participant's score: <see cref="Primary"/> (<c>2</c>), an optional <see cref="Secondary"/> (a shoot-out's
/// <c>4</c>), the breakdown by period and status badges.
/// </summary>
public sealed record ParticipantScore(Guid ParticipantId, string Side, string Primary)
{
    public string? Secondary { get; init; }

    public IReadOnlyList<PeriodScore> Periods { get; init; } = [];

    public IReadOnlyList<string> Badges { get; init; } = [];
}

/// <summary>A period's share of the score, labelled with the period's <c>short</c> label (<c>1H</c>).</summary>
public sealed record PeriodScore(string PeriodCode, int Occurrence, string ShortLabel, string Value);
