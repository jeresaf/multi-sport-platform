using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>The registered engines by family, and the fold every family shares (sport-engine.md §4).</summary>
public sealed class ScoringEngines(IEnumerable<IScoringEngine> engines) : IScoringEngines
{
    private readonly Dictionary<string, IScoringEngine> _engines = engines.ToDictionary(engine => engine.Family, StringComparer.Ordinal);

    public IScoringEngine EngineFor(SportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return _engines.TryGetValue(definition.ScoringFamily, out var engine)
            ? engine
            : throw new InvalidOperationException($"No engine computes the scoring family {definition.ScoringFamily}.");
    }

    public ContestLiveState Fold(ContestContext ctx, ContestStatus status, IReadOnlyList<TimelineEvent> timeline)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var entries = TimelineFold.Resolve(timeline);
        var active = TimelineFold.Active(entries);
        var engine = EngineFor(ctx.Definition);
        var score = active.Aggregate(engine.Initial(ctx), (state, evt) => engine.Apply(ctx, state, evt));
        return new ContestLiveState(status, score, PhaseTracker.Track(ctx.Definition, active), PersonnelTracker.Track(ctx, active), entries);
    }
}
