using System.Collections.Immutable;
using System.Globalization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>
/// The <c>ADDITIVE</c> family (sport-engine.md §5): each active event's <c>points</c> go to the acting participant or
/// its opponent, in the score component the event type names. S05-T2a: validation knows event types only, and the
/// outcome compares <c>main</c> totals; the §5 checks and tie resolution follow in T2b and T2c.
/// </summary>
public sealed class AdditiveEngine : IScoringEngine
{
    public const string FamilyCode = "ADDITIVE";

    public const string ShootoutComponent = "shootout";

    public string Family => FamilyCode;

    public ScoreState Initial(ContestContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        return new AdditiveScoreState(
            ctx.Sides.ToImmutableDictionary(side => side.ParticipantId, _ => AdditiveSideScore.Empty),
            []);
    }

    public ValidationResult Validate(ContestContext ctx, ContestLiveState live, TimelineEventDraft draft)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(draft);
        var known = ctx.Definition.EventTypes.Any(type => type.Code == draft.EventType)
            || ctx.Definition.GenericEventTypes.Any(type => type.Code == draft.EventType);
        return known ? ValidationResult.Valid : ValidationResult.Invalid(SportsConfigurationErrors.TimelineEventUnknown);
    }

    public ScoreState Apply(ContestContext ctx, ScoreState state, TimelineEvent evt)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(evt);
        var score = state as AdditiveScoreState ?? throw new ArgumentException($"Expected the {FamilyCode} state.", nameof(state));
        if (evt.EventType == GenericEventCodes.PeriodStart && evt.PeriodCode is { } started)
        {
            return score with { Periods = score.Periods.Add(new PlayedPeriod(started, evt.PeriodOccurrence)) };
        }

        var type = ctx.Definition.EventTypes.FirstOrDefault(candidate => candidate.Code == evt.EventType);
        if (type is not { Points: { } points and not 0 })
        {
            return score;
        }

        var actor = evt.ParticipantId ?? throw new InvalidOperationException($"Event {evt.Id} scores but has no participant.");
        var receiver = type.Side == ActorSide.Opponent ? ctx.OpponentOf(actor) : actor;
        var period = evt.PeriodCode is { } code ? new PlayedPeriod(code, evt.PeriodOccurrence) : null;
        var component = type.Component ?? ScoringFamilies.MainComponent;
        return score with
        {
            Participants = score.Participants.SetItem(receiver, score.For(receiver).Add(component, period, points)),
        };
    }

    // S05-T2a: level main totals are a draw; tie resolution (extra periods, shoot-out, sudden death) comes in T2c.
    public OutcomeProposal ProposeOutcome(ContestContext ctx, ScoreState state)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var score = state as AdditiveScoreState ?? throw new ArgumentException($"Expected the {FamilyCode} state.", nameof(state));
        if (ctx.Sides.Count != 2)
        {
            throw new InvalidOperationException($"The {FamilyCode} family scores head-to-head contests only.");
        }

        var (first, second) = (ctx.Sides[0].ParticipantId, ctx.Sides[1].ParticipantId);
        var difference = score.For(first).Total - score.For(second).Total;
        return difference == 0
            ? OutcomeProposal.Draw
            : OutcomeProposal.Win(difference > 0 ? first : second, OutcomeMethod.Regulation);
    }

    public ScoreSummary Summarize(ContestContext ctx, ScoreState state)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var score = state as AdditiveScoreState ?? throw new ArgumentException($"Expected the {FamilyCode} state.", nameof(state));
        var periods = score.Periods.Select(played => (Played: played, Definition: PhaseTracker.Period(ctx.Definition, played.PeriodCode))).ToList();
        var shootout = periods.Any(period => period.Definition.Kind == PhaseKind.Shootout);
        return new ScoreSummary([.. ctx.Sides.Select(side =>
        {
            var points = score.For(side.ParticipantId);
            return new ParticipantScore(side.ParticipantId, side.Side, Text(points.Total))
            {
                Secondary = shootout ? Text(points.Component(ShootoutComponent)) : null,
                Periods = [.. periods
                    .Where(period => period.Definition.Kind == PhaseKind.Play)
                    .Select(period => new PeriodScore(
                        period.Played.PeriodCode, period.Played.Occurrence, period.Definition.ShortLabel, Text(points.MainByPeriod.GetValueOrDefault(period.Played))))],
            };
        })]);
    }

    private static string Text(int points) => points.ToString(CultureInfo.InvariantCulture);
}
