using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>The current period from the active <c>PERIOD_START</c> and <c>PERIOD_END</c> events (sport-engine.md §4).</summary>
public static class PhaseTracker
{
    /// <summary>
    /// Follows the active events in sequence order. A start opens the period it names, as the next occurrence of that
    /// code; an end closes the running period. Whether a start or an end was allowed is the engine's check, not this.
    /// </summary>
    public static ContestPhase Track(SportDefinition definition, IEnumerable<TimelineEvent> active)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(active);
        var phase = ContestPhase.NotStarted;
        var played = new List<PlayedPeriod>();
        foreach (var evt in active)
        {
            if (evt.EventType == GenericEventCodes.PeriodStart)
            {
                var period = Period(definition, evt.PeriodCode);
                var occurrence = played.Count(item => item.PeriodCode == period.Code) + 1;
                played.Add(new PlayedPeriod(period.Code, occurrence));
                phase = new ContestPhase(period.Code, occurrence, Running: true, period.Kind) { Played = [.. played] };
            }
            else if (evt.EventType == GenericEventCodes.PeriodEnd && phase.Running)
            {
                phase = phase with { Running = false, Kind = PhaseKind.Break };
            }
        }

        return phase;
    }

    public static PeriodDefinition Period(SportDefinition definition, string? code) =>
        definition.Periods.FirstOrDefault(period => period.Code == code)
            ?? throw new InvalidOperationException($"{definition.Variant}@{definition.Version} has no period \"{code}\".");
}
