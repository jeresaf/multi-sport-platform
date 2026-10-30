using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>
/// Who is on the field, on the bench, suspended, replaced or dismissed per participant, from the lineups and the
/// effects of the active events (sport-engine.md §4). Roles are read from the definition, never by name: an effect
/// applies to the event's first declared actor role that is filled, and a substitution takes off the player in the
/// role drawn from the field and brings on the one in the role drawn from the bench.
/// </summary>
public static class PersonnelTracker
{
    public static ContestPersonnel Track(ContestContext ctx, IEnumerable<TimelineEvent> active)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(active);
        var people = ctx.Sides.ToDictionary(side => side.ParticipantId, side => new People(ctx.Lineups.FirstOrDefault(lineup => lineup.ParticipantId == side.ParticipantId)));
        foreach (var evt in active)
        {
            var type = ctx.Definition.EventTypes.FirstOrDefault(candidate => candidate.Code == evt.EventType);
            if (type is null || type.Effects.Count == 0 || evt.ParticipantId is not { } participantId || !people.TryGetValue(participantId, out var side))
            {
                continue;
            }

            foreach (var effect in type.Effects)
            {
                Apply(ctx.Definition, side, type, evt, effect);
            }
        }

        return new ContestPersonnel(people.ToDictionary(pair => pair.Key, pair => pair.Value.ToRecord()));
    }

    private static void Apply(SportDefinition definition, People side, EventTypeDefinition type, TimelineEvent evt, EventEffect effect)
    {
        var subject = type.Actors.Select(actor => evt.Actors.TryGetValue(actor.Role, out var id) ? id : (Guid?)null).FirstOrDefault(id => id is not null);
        switch (effect)
        {
            case EventEffect.SendOff or EventEffect.Disqualify when subject is { } dismissed:
                side.MoveTo(side.Dismissed, dismissed);
                break;
            case EventEffect.TemporarySuspension when subject is { } suspended:
                side.Suspend(suspended);
                break;
            case EventEffect.Substitute or EventEffect.TemporarySubstitute:
                var off = ActorFrom(definition, type, evt, ActorPool.OnField);
                var on = ActorFrom(definition, type, evt, ActorPool.Bench);
                if (off is { } playerOff)
                {
                    if (effect == EventEffect.Substitute)
                    {
                        side.MoveTo(side.Replaced, playerOff);
                    }
                    else if (on is { } replacement)
                    {
                        side.Remove(playerOff);
                        side.TemporarilyReplaced[playerOff] = replacement;
                    }
                }

                if (on is { } playerOn)
                {
                    side.MoveTo(side.OnField, playerOn);
                }

                break;
            case EventEffect.Return when subject is { } returning:
                side.Return(returning);
                break;
            case EventEffect.FoulCount when subject is { } offender:
                side.Fouls[offender] = side.Fouls.GetValueOrDefault(offender) + 1;
                break;
        }
    }

    private static Guid? ActorFrom(SportDefinition definition, EventTypeDefinition type, TimelineEvent evt, ActorPool pool) =>
        type.Actors
            .Where(actor => definition.ActorRoles.TryGetValue(actor.Role, out var role) && role.Who == pool)
            .Select(actor => evt.Actors.TryGetValue(actor.Role, out var id) ? id : (Guid?)null)
            .FirstOrDefault(id => id is not null);

    private sealed class People
    {
        public People(ContestLineup? lineup)
        {
            OnField = [.. lineup?.Starters ?? []];
            Bench = [.. lineup?.Bench ?? []];
            Staff = [.. lineup?.Staff ?? []];
        }

        public HashSet<Guid> OnField { get; }

        public HashSet<Guid> Bench { get; }

        public HashSet<Guid> Staff { get; }

        public HashSet<Guid> Suspended { get; } = [];

        public HashSet<Guid> Replaced { get; } = [];

        public HashSet<Guid> Dismissed { get; } = [];

        public Dictionary<Guid, Guid> TemporarilyReplaced { get; } = [];

        public Dictionary<Guid, int> Fouls { get; } = [];

        // Where each suspended person was, so they return there: a bench player or staff member is never put on the field.
        private Dictionary<Guid, HashSet<Guid>> SuspendedFrom { get; } = [];

        // Someone already off for good or for a while (replaced, dismissed, temporarily replaced) stays where they are:
        // suspending them would lose that, and their return would put them on the field (review #78 N1). Someone the
        // lineup doesn't name (below STANDARD) is taken to be on the field.
        public void Suspend(Guid person)
        {
            if (Replaced.Contains(person) || Dismissed.Contains(person) || TemporarilyReplaced.ContainsKey(person))
            {
                return;
            }

            var from = Bench.Contains(person) ? Bench : Staff.Contains(person) ? Staff : OnField;
            MoveTo(Suspended, person);
            SuspendedFrom[person] = from;
        }

        public void MoveTo(HashSet<Guid> target, Guid person)
        {
            Remove(person);
            target.Add(person);
        }

        public void Remove(Guid person)
        {
            OnField.Remove(person);
            Bench.Remove(person);
            Staff.Remove(person);
            Suspended.Remove(person);
            Replaced.Remove(person);
            Dismissed.Remove(person);
            TemporarilyReplaced.Remove(person);
            SuspendedFrom.Remove(person);
        }

        // Back from suspension, or from a temporary substitution: named either as the player who left or as the one
        // who replaced them, the player who left comes back. The replacement returns to the bench only from the field:
        // one sent off, suspended or replaced meanwhile stays where that left them.
        public void Return(Guid person)
        {
            if (Suspended.Contains(person))
            {
                MoveTo(SuspendedFrom.GetValueOrDefault(person, OnField), person);
                return;
            }

            var pair = TemporarilyReplaced.FirstOrDefault(item => item.Key == person || item.Value == person);
            if (TemporarilyReplaced.ContainsKey(pair.Key))
            {
                if (OnField.Contains(pair.Value))
                {
                    MoveTo(Bench, pair.Value);
                }

                MoveTo(OnField, pair.Key);
            }
        }

        public ParticipantPersonnel ToRecord() => new(
            OnField.ToHashSet(), Bench.ToHashSet(), Staff.ToHashSet(), Suspended.ToHashSet(), Replaced.ToHashSet(), Dismissed.ToHashSet())
        {
            TemporarilyReplaced = new Dictionary<Guid, Guid>(TemporarilyReplaced),
            Fouls = new Dictionary<Guid, int>(Fouls),
        };
    }
}
