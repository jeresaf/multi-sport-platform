using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>
/// The checks every family runs before an event may be recorded (sport-engine.md §5, Validate 1–3, and the corrections
/// of §9), each read from the definition, never by sport. They run in groups, and the first group that fails answers:
/// the event type, the contest status, the corrected or reversed event, the phase, the participant, the actors, then
/// the follow-up and the limits (<see cref="FollowUpsAndLimits"/>). A
/// correction (<c>supersedes</c>) is checked as at the event it replaces: the phase and the people are folded from the
/// active events before it (owner, 2026-10-30), so a first-half scorer can be corrected at half-time.
/// </summary>
public static class TimelineChecks
{
    public static ValidationResult Validate(ContestContext ctx, ContestLiveState live, TimelineEventDraft draft)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(draft);
        var type = Type(ctx.Definition, draft.EventType);
        if (type is null && !ctx.Definition.GenericEventTypes.Any(candidate => candidate.Code == draft.EventType))
        {
            return ValidationResult.Invalid(SportsConfigurationErrors.TimelineEventUnknown);
        }

        if (!StatusAllows(live, draft))
        {
            return ValidationResult.Invalid(SportsConfigurationErrors.TimelineContestNotLive);
        }

        if (References(ctx, live, draft) is not { } basis)
        {
            return ValidationResult.Invalid(SportsConfigurationErrors.TimelineReferenceInvalid);
        }

        if (!PhaseAllows(ctx.Definition, basis.Phase, type, draft))
        {
            return ValidationResult.Invalid(SportsConfigurationErrors.TimelineEventNotAllowedInPhase);
        }

        if (!ParticipantValid(ctx, type, draft))
        {
            return ValidationResult.Invalid(SportsConfigurationErrors.TimelineParticipantInvalid);
        }

        var actorErrors = ActorErrors(ctx, basis.Personnel, type, draft);
        if (actorErrors.Count > 0 || type is null)
        {
            return new ValidationResult(actorErrors);
        }

        // §5.4. A correction belongs to the period of the event it replaces, a new event to the current one.
        var period = basis.Replaced is { PeriodCode: { } code } replaced
            ? new PlayedPeriod(code, replaced.PeriodOccurrence)
            : basis.Phase.PeriodCode is { } current ? new PlayedPeriod(current, basis.Phase.Occurrence) : null;
        return new ValidationResult([.. new[]
        {
            FollowUpsAndLimits.FollowUpError(ctx.Definition, type, draft, period, basis.Before),
            FollowUpsAndLimits.LimitError(ctx, type, draft, ByOrigin(live), basis.Replaced),
        }.OfType<Error>()]);
    }

    // What the later checks read: the phase and the people, the active events before the draft, and for a correction
    // the event it replaces.
    private sealed record Basis(ContestPhase Phase, ContestPersonnel Personnel, IReadOnlyList<TimelineEvent> Before)
    {
        public TimelineEvent? Replaced { get; init; }
    }

    // §5.1 with §9: live; or ended with the correction window still open, for corrections and for reversals of anything
    // but a period's start or end (a new period or goal after the end, or a period running again, would reopen the
    // contest, which is ENDED → LIVE); a backfill or an administrator records at any time.
    private static bool StatusAllows(ContestLiveState live, TimelineEventDraft draft) =>
        draft.Source is TimelineSource.Backfill or TimelineSource.Admin
            || live.Status == ContestStatus.Live
            || (live.Status == ContestStatus.Ended && live.CorrectionWindowOpen
                && (draft.SupersedesEventId is not null
                    || (draft.ReversesEventId is { } reverses && !ReversesPeriod(live, reverses))));

    // Reversing a reversal restores what it had undone, so the chain is followed down to the event it ends at.
    private static bool ReversesPeriod(ContestLiveState live, Guid id)
    {
        var target = live.Timeline.FirstOrDefault(entry => entry.Event.Id == id)?.Event;
        while (target?.ReversesEventId is { } undone)
        {
            target = live.Timeline.FirstOrDefault(entry => entry.Event.Id == undone)?.Event;
        }

        return target?.EventType is GenericEventCodes.PeriodStart or GenericEventCodes.PeriodEnd;
    }

    // §9: a reversal names an active event whose type lists the reversing type in reversible_by (EVENT_REVERSED
    // reverses anything) and is checked in the current phase; a correction replaces an active event of the definition
    // with another one of any of its types, never a generic event (a period start isn't corrected, it is reversed).
    // Null when the reference is invalid; otherwise what the later checks read: the current state, or for a correction
    // the state just before the event its chain of corrections started from. There, every active event stands where
    // its own chain started, so an earlier event corrected later still counts, in its corrected form.
    private static Basis? References(ContestContext ctx, ContestLiveState live, TimelineEventDraft draft)
    {
        if (draft.ReversesEventId is { } reverses)
        {
            var target = Active(live, reverses);
            var allowed = draft.SupersedesEventId is null
                && target is not null
                && (draft.EventType == GenericEventCodes.EventReversed
                    || (Type(ctx.Definition, target.EventType)?.ReversibleBy.Contains(draft.EventType) ?? false));
            return allowed ? Current(live) : null;
        }

        if (draft.EventType == GenericEventCodes.EventReversed)
        {
            return null;
        }

        if (draft.SupersedesEventId is { } supersedes)
        {
            var target = Active(live, supersedes);
            if (target is null || Type(ctx.Definition, target.EventType) is null || Type(ctx.Definition, draft.EventType) is null)
            {
                return null;
            }

            var original = Original(live, target).Sequence;
            var before = ByOrigin(live).Where(evt => Original(live, evt).Sequence < original).ToList();
            return new Basis(PhaseTracker.Track(ctx.Definition, before), PersonnelTracker.Track(ctx, before), before) { Replaced = target };
        }

        return Current(live);
    }

    private static Basis Current(ContestLiveState live) => new(live.Phase, live.Personnel, ByOrigin(live));

    // The active events, each where its chain of corrections started: a try corrected after its conversion still comes
    // before it, and a corrected substitution stays in its window.
    private static List<TimelineEvent> ByOrigin(ContestLiveState live) =>
        [.. TimelineFold.Active(live.Timeline).OrderBy(evt => Original(live, evt).Sequence)];

    // §5.2: a definition's event type lists the phase kinds it may be recorded in; nothing of the sport happens before
    // the first period starts. A start needs a period of the definition and none running; an end needs one running.
    // The other generic events (clock, status, notes, reversals) may come at any time. Which period may start next is
    // the tie resolution's concern (S05-T2c).
    private static bool PhaseAllows(SportDefinition definition, ContestPhase phase, EventTypeDefinition? type, TimelineEventDraft draft)
    {
        if (type is not null)
        {
            return phase.Kind is { } kind && type.Phases.Contains(kind);
        }

        return draft.EventType switch
        {
            GenericEventCodes.PeriodStart => !phase.Running && definition.Periods.Any(period => period.Code == draft.PeriodCode),
            GenericEventCodes.PeriodEnd => phase.Running,
            _ => true,
        };
    }

    // An event of a side (it scores, names people, has effects, limits or a follow-up) needs one of the contest's
    // participants; any other event may name one, but only one of the contest's.
    private static bool ParticipantValid(ContestContext ctx, EventTypeDefinition? type, TimelineEventDraft draft)
    {
        if (draft.ParticipantId is { } participant)
        {
            return ctx.Sides.Any(side => side.ParticipantId == participant);
        }

        return type is null
            || type is { Points: null, Side: null, Actors.Count: 0, Effects.Count: 0, Limits.Count: 0, FollowUp: null };
    }

    // §5.3: each named role must be one of the type's, and distinct from the role it must differ from; a role is
    // required from its required_from coverage, or always when it is neither optional nor has one. From STANDARD
    // coverage the lineups are known, so each person must be where the role draws from (field, bench, squad, staff) on
    // the role's side; below it nobody is checked against the lineups.
    private static List<Error> ActorErrors(ContestContext ctx, ContestPersonnel personnel, EventTypeDefinition? type, TimelineEventDraft draft)
    {
        var roles = type?.Actors ?? [];
        var errors = new List<Error>();
        if (roles.Any(role => Required(role, ctx.Coverage) && !draft.Actors.ContainsKey(role.Role)))
        {
            errors.Add(SportsConfigurationErrors.TimelineActorRequired);
        }

        var invalid = draft.Actors.Keys.Any(role => roles.All(declared => declared.Role != role))
            || roles.Any(role => role.DistinctFrom is { } other
                && draft.Actors.TryGetValue(role.Role, out var person)
                && draft.Actors.TryGetValue(other, out var otherPerson)
                && person == otherPerson);
        if (invalid)
        {
            errors.Add(SportsConfigurationErrors.TimelineActorInvalid);
            return errors;
        }

        if (ctx.Coverage < CoverageLevel.Standard || draft.ParticipantId is not { } participant)
        {
            return errors;
        }

        foreach (var (role, person) in draft.Actors)
        {
            var definition = ctx.Definition.ActorRoles[role];
            var people = personnel.For(definition.Side == ActorSide.Opponent ? ctx.OpponentOf(participant) : participant);
            if (!InPool(people, definition.Who, person))
            {
                var error = definition.Who == ActorPool.OnField
                    ? SportsConfigurationErrors.TimelineActorNotOnField
                    : SportsConfigurationErrors.TimelineActorInvalid;
                if (!errors.Contains(error))
                {
                    errors.Add(error);
                }
            }
        }

        return errors;
    }

    private static bool Required(EventActorDefinition role, CoverageLevel coverage) =>
        role.RequiredFrom is { } from ? coverage >= from : !role.Optional;

    // The squad is the participant's players still in the contest's lineup: on the field, on the bench, suspended,
    // temporarily replaced or replaced; a dismissed person is in no pool.
    private static bool InPool(ParticipantPersonnel people, ActorPool pool, Guid person) => pool switch
    {
        ActorPool.OnField => people.OnField.Contains(person),
        ActorPool.Bench => people.Bench.Contains(person),
        ActorPool.Staff => people.Staff.Contains(person),
        ActorPool.Squad => InSquad(people, person),
        ActorPool.SquadOrStaff => InSquad(people, person) || people.Staff.Contains(person),
        _ => throw new InvalidOperationException($"Actor pool {pool} has no check."),
    };

    private static bool InSquad(ParticipantPersonnel people, Guid person) =>
        people.OnField.Contains(person) || people.Bench.Contains(person) || people.Suspended.Contains(person)
            || people.TemporarilyReplaced.ContainsKey(person) || people.Replaced.Contains(person);

    // A correction of a correction is checked as at the first event of the chain, not when the earlier one was entered.
    private static TimelineEvent Original(ContestLiveState live, TimelineEvent evt)
    {
        while (evt.SupersedesEventId is { } replaced && live.Timeline.FirstOrDefault(entry => entry.Event.Id == replaced)?.Event is { } earlier)
        {
            evt = earlier;
        }

        return evt;
    }

    private static TimelineEvent? Active(ContestLiveState live, Guid id) =>
        live.Timeline.FirstOrDefault(entry => entry.Event.Id == id && entry.Status == EffectiveStatus.Active)?.Event;

    private static EventTypeDefinition? Type(SportDefinition definition, string code) =>
        definition.EventTypes.FirstOrDefault(candidate => candidate.Code == code);
}
