using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>Effective status of every event (sport-engine.md §3), shared by every family.</summary>
public static class TimelineFold
{
    /// <summary>
    /// The timeline in sequence order with each event's effective status. Only a later event counts, and only one that
    /// still stands: an event is <c>REVERSED</c> by a later active event that reverses it, else <c>SUPERSEDED</c> by a
    /// later event that supersedes it and is not itself reversed (a superseded correction still replaces what it
    /// corrected, so a chain of corrections ends at its last one). Reversing a reversal or a correction restores what
    /// it had undone.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> Resolve(IReadOnlyList<TimelineEvent> timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var ordered = timeline.OrderBy(evt => evt.Sequence).ToList();
        if (ordered.Select(evt => evt.Sequence).Distinct().Count() != ordered.Count || ordered.Select(evt => evt.Id).Distinct().Count() != ordered.Count)
        {
            throw new ArgumentException("Timeline events must have distinct ids and sequences.", nameof(timeline));
        }

        var reversed = new HashSet<Guid>();
        var superseded = new HashSet<Guid>();
        var statuses = new EffectiveStatus[ordered.Count];
        // Latest first: when an event is reached, every later event already has its final status.
        for (var index = ordered.Count - 1; index >= 0; index--)
        {
            var evt = ordered[index];
            var status = reversed.Contains(evt.Id) ? EffectiveStatus.Reversed
                : superseded.Contains(evt.Id) ? EffectiveStatus.Superseded
                : EffectiveStatus.Active;
            statuses[index] = status;
            if (status == EffectiveStatus.Active && evt.ReversesEventId is { } reverses)
            {
                reversed.Add(reverses);
            }

            if (status != EffectiveStatus.Reversed && evt.SupersedesEventId is { } supersedes)
            {
                superseded.Add(supersedes);
            }
        }

        return [.. ordered.Select((evt, index) => new TimelineEntry(evt, statuses[index]))];
    }

    public static IReadOnlyList<TimelineEvent> Active(IEnumerable<TimelineEntry> entries) =>
        [.. entries.Where(entry => entry.Status == EffectiveStatus.Active).Select(entry => entry.Event)];
}
