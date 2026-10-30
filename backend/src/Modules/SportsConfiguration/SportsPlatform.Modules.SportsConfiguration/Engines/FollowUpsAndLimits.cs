using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>
/// sport-engine.md §5, Validate 4: an event type's follow-up (it needs an earlier event, such as a conversion after a
/// try) and its limits (rules such as <c>substitutions.max</c>), both read from the definition and the rules, never by
/// sport. <see cref="TimelineChecks"/> runs them last.
/// </summary>
public static class FollowUpsAndLimits
{
    /// <summary>The limit kinds the engines know, by the rule's last segment (<c>substitutions.max</c>).</summary>
    public const string MaxKind = "max";

    public const string WindowsKind = "windows";

    /// <summary>A rule next to a <c>max</c> limit that adds to it once an overtime period has started.</summary>
    public const string ExtraInExtraTime = "extra_in_extra_time";

    /// <summary>
    /// A follow-up needs an earlier active event of a type it lists, by the same participant or in the same period when
    /// it says so, and not yet used by an event that consumes one when it says so; each event that consumes takes the
    /// latest earlier event its follow-up accepts. The draft is put in its place (a correction where the event it
    /// replaces stood, a new event at the end) and the whole timeline is walked: the draft must find its earlier event,
    /// and every follow-up that had one must keep it, so a correction can't give a try a second conversion or move a
    /// converted try away from its conversion (review #80, owner 2026-10-30). <paramref name="active"/> is the active
    /// timeline, each event where its chain of corrections started.
    /// </summary>
    public static Error? FollowUpError(
        SportDefinition definition, TimelineEventDraft draft, PlayedPeriod? period, IReadOnlyList<TimelineEvent> active, TimelineEvent? replaced)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(active);
        // A new event without a follow-up comes last, so it takes nothing from the follow-ups before it.
        if (Type(definition, draft.EventType)?.FollowUp is null && replaced is null)
        {
            return null;
        }

        var candidate = Candidate(draft, period);
        var unmet = Unmet(definition, active);
        var after = Unmet(definition, WithDraft(active, replaced, candidate));
        return after.Contains(candidate.Id) || after.Any(id => !unmet.Contains(id)) ? SportsConfigurationErrors.TimelineFollowUpMissing : null;
    }

    /// <summary>
    /// Each limit from the rules; a rule the rules don't set is no limit. <c>…max</c> counts the participant's events of
    /// every type with that limit, plus <c>…extra_in_extra_time</c> once an overtime period has started.
    /// <c>…windows</c> counts the participant's runs of such events in play: a run ends at a period's start or end, at
    /// another event of the definition (any participant's events with the limit don't end it), or when the
    /// participant's next one has another game-clock minute (owner, 2026-10-30; an event without a clock neither ends
    /// a run nor starts a new one), and events in a break use no window. The draft is put in its place as for
    /// follow-ups. The draft's limits are checked for its participant, and for a correction also the replaced event's
    /// for its participant, since changing a substitution into another event can split a window (review #80).
    /// </summary>
    public static Error? LimitError(ContestContext ctx, TimelineEventDraft draft, IReadOnlyList<TimelineEvent> active, TimelineEvent? replaced)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(active);
        var checks = Limits(ctx.Definition, draft.EventType, draft.ParticipantId)
            .Concat(replaced is null ? [] : Limits(ctx.Definition, replaced.EventType, replaced.ParticipantId))
            .Distinct()
            .ToList();
        if (checks.Count == 0)
        {
            return null;
        }

        var steps = InPhase(ctx.Definition, WithDraft(active, replaced, Candidate(draft, period: null)));
        foreach (var (rule, participant) in checks)
        {
            if (ctx.Rules.Number(rule) is not { } value)
            {
                continue;
            }

            var cut = rule.LastIndexOf('.');
            var (group, kind) = cut < 0 ? (string.Empty, rule) : (rule[..cut], rule[(cut + 1)..]);
            var limited = steps.Select(step => Limited(ctx.Definition, step.Event, rule) && step.Event.ParticipantId == participant).ToList();
            var used = kind switch
            {
                MaxKind => limited.Count(flag => flag),
                WindowsKind => Windows(ctx.Definition, steps, limited, rule),
                _ => throw new InvalidOperationException(
                    $"Limit {rule}: the engines don't know the kind \"{kind}\" (it comes with its sport's slice)."),
            };
            // Once extra time has started, the extra one applies to the whole contest, so a correction of an earlier event
            // is counted against the same limit as a new one (owner, 2026-10-30).
            var allowed = kind == MaxKind && steps.Any(step => StartsOvertime(ctx.Definition, step.Event))
                ? value + (ctx.Rules.Number($"{group}.{ExtraInExtraTime}") ?? 0)
                : value;
            if (used > allowed)
            {
                return SportsConfigurationErrors.TimelineLimitReached;
            }
        }

        return null;
    }

    // The draft as an event: it has no id or sequence yet (the empty id stands for it), only what the checks read.
    private static TimelineEvent Candidate(TimelineEventDraft draft, PlayedPeriod? period) =>
        new(Guid.Empty, long.MaxValue, draft.EventType, draft.Source)
        {
            ParticipantId = draft.ParticipantId,
            PeriodCode = period?.PeriodCode,
            PeriodOccurrence = period?.Occurrence ?? 0,
            GameClockSeconds = draft.GameClockSeconds,
            AddedTimeSeconds = draft.AddedTimeSeconds,
        };

    // A correction takes the place of the event it replaces (References has checked that it is active); a new event comes last.
    private static List<TimelineEvent> WithDraft(IReadOnlyList<TimelineEvent> active, TimelineEvent? replaced, TimelineEvent candidate) =>
        // LINQ, not a spread: the compiler writes a spread into a list with CollectionsMarshal, which the engine rule bans.
        replaced is null ? active.Append(candidate).ToList() : active.Select(evt => evt.Id == replaced.Id ? candidate : evt).ToList();

    private static IEnumerable<(string Rule, Guid Participant)> Limits(SportDefinition definition, string eventType, Guid? participant) =>
        participant is { } id && Type(definition, eventType) is { } type ? type.Limits.Select(limit => (limit.Rule, id)) : [];

    // Walks the events in order: the follow-ups that find no earlier event to follow, by id.
    private static HashSet<Guid> Unmet(SportDefinition definition, IReadOnlyList<TimelineEvent> events)
    {
        var consumed = new HashSet<Guid>();
        var unmet = new HashSet<Guid>();
        for (var index = 0; index < events.Count; index++)
        {
            var evt = events[index];
            if (Type(definition, evt.EventType)?.FollowUp is not { } followUp)
            {
                continue;
            }

            if (Preceding(followUp, evt.ParticipantId, Period(evt), events.Take(index).ToList(), consumed) is not { } used)
            {
                unmet.Add(evt.Id);
            }
            else if (followUp.Consumes)
            {
                consumed.Add(used.Id);
            }
        }

        return unmet;
    }

    private static TimelineEvent? Preceding(
        FollowUpDefinition followUp, Guid? participant, PlayedPeriod? period, IReadOnlyList<TimelineEvent> before, HashSet<Guid> consumed) =>
        before.LastOrDefault(evt => followUp.RequiresPreceding.Contains(evt.EventType)
            && (!followUp.SameSide || evt.ParticipantId == participant)
            && (!followUp.SamePeriod || Period(evt) == period)
            && (!followUp.Unconsumed || !consumed.Contains(evt.Id)));

    private static int Windows(SportDefinition definition, List<(TimelineEvent Event, PhaseKind? Kind)> steps, List<bool> limited, string rule)
    {
        var windows = 0;
        var open = false;
        int? minute = null;
        for (var index = 0; index < steps.Count; index++)
        {
            var (evt, kind) = steps[index];
            if (limited[index])
            {
                var at = Minute(evt);
                if (kind == PhaseKind.Play && (!open || (at is not null && minute is not null && at != minute)))
                {
                    windows++;
                    open = true;
                }

                minute = at ?? minute;
            }
            else if (evt.EventType is GenericEventCodes.PeriodStart or GenericEventCodes.PeriodEnd
                || (Type(definition, evt.EventType) is not null && !Limited(definition, evt, rule)))
            {
                open = false;
            }
        }

        return windows;
    }

    // Each event with the phase kind it was recorded in, as the phase tracker reads the starts and ends.
    private static List<(TimelineEvent Event, PhaseKind? Kind)> InPhase(SportDefinition definition, IReadOnlyList<TimelineEvent> events)
    {
        var steps = new List<(TimelineEvent, PhaseKind?)>();
        PhaseKind? kind = null;
        foreach (var evt in events)
        {
            if (evt.EventType == GenericEventCodes.PeriodStart)
            {
                kind = PhaseTracker.Period(definition, evt.PeriodCode).Kind;
            }
            else if (evt.EventType == GenericEventCodes.PeriodEnd && kind is not null)
            {
                kind = PhaseKind.Break;
            }

            steps.Add((evt, kind));
        }

        return steps;
    }

    // The minute on the game clock, added time included (45+2 is the 47th); null when the event has no clock.
    private static int? Minute(TimelineEvent evt) =>
        evt.GameClockSeconds is { } clock ? (clock + (evt.AddedTimeSeconds ?? 0)) / 60 : null;

    private static bool Limited(SportDefinition definition, TimelineEvent evt, string rule) =>
        Type(definition, evt.EventType)?.Limits.Any(limit => limit.Rule == rule) ?? false;

    private static bool StartsOvertime(SportDefinition definition, TimelineEvent evt) =>
        evt.EventType == GenericEventCodes.PeriodStart && PhaseTracker.Period(definition, evt.PeriodCode) is { Overtime: true, Kind: PhaseKind.Play };

    private static PlayedPeriod? Period(TimelineEvent evt) =>
        evt.PeriodCode is { } code ? new PlayedPeriod(code, evt.PeriodOccurrence) : null;

    private static EventTypeDefinition? Type(SportDefinition definition, string code) =>
        definition.EventTypes.FirstOrDefault(candidate => candidate.Code == code);
}
