using System.Collections.Immutable;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>
/// The <c>ADDITIVE</c> state (sport-engine.md §5): per participant, the points in each score component and the
/// <c>main</c> points by period; and every period started, in order, so the summary can show the ones with no score.
/// </summary>
public sealed record AdditiveScoreState(
    ImmutableDictionary<Guid, AdditiveSideScore> Participants,
    ImmutableList<PlayedPeriod> Periods) : ScoreState(AdditiveEngine.FamilyCode)
{
    public AdditiveSideScore For(Guid participantId) => Participants.GetValueOrDefault(participantId, AdditiveSideScore.Empty);
}

public sealed record AdditiveSideScore(
    ImmutableDictionary<string, int> Components,
    ImmutableDictionary<PlayedPeriod, int> MainByPeriod)
{
    public static AdditiveSideScore Empty { get; } = new(
        ImmutableDictionary.Create<string, int>(StringComparer.Ordinal), ImmutableDictionary<PlayedPeriod, int>.Empty);

    /// <summary>The <c>main</c> component, which decides the winner.</summary>
    public int Total => Component(ScoringFamilies.MainComponent);

    public int Component(string component) => Components.GetValueOrDefault(component);

    public AdditiveSideScore Add(string component, PlayedPeriod? period, int points) => this with
    {
        Components = Components.SetItem(component, Component(component) + points),
        MainByPeriod = component == ScoringFamilies.MainComponent && period is not null
            ? MainByPeriod.SetItem(period, MainByPeriod.GetValueOrDefault(period) + points)
            : MainByPeriod,
    };
}
