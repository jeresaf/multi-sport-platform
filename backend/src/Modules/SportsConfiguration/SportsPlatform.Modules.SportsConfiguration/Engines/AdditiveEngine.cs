using System.Collections.Immutable;
using System.Globalization;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Engines;

/// <summary>
/// The <c>ADDITIVE</c> family (sport-engine.md §5): each active event's <c>points</c> go to the acting participant or
/// its opponent, in the score component the event type names. Validation runs the checks every family shares
/// (<see cref="TimelineChecks"/>); the outcome applies the rules' tie resolution.
/// </summary>
public sealed class AdditiveEngine : IScoringEngine
{
    public const string FamilyCode = ScoringFamilies.Additive;

    public const string ShootoutComponent = "shootout";

    /// <summary>The rule that overrides an event type's points: <c>scoring.points.&lt;EVENT_TYPE&gt;</c>.</summary>
    public const string PointsRulePrefix = "scoring.points.";

    public const string TieResolutionRule = "contest.tie_resolution";

    public const string ExtraPeriodSequenceRule = "contest.extra_period_sequence";

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
        return TimelineChecks.Validate(ctx, live, draft);
    }

    public ScoreState Apply(ContestContext ctx, ScoreState state, TimelineEvent evt)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(evt);
        var score = state as AdditiveScoreState ?? throw new ArgumentException($"Expected the {FamilyCode} state.", nameof(state));
        if (evt.EventType == GenericEventCodes.PeriodStart && evt.PeriodCode is { } started)
        {
            return score with { Periods = score.Periods.Add(new PlayedPeriod(started, evt.PeriodOccurrence)), PeriodRunning = true };
        }

        if (evt.EventType == GenericEventCodes.PeriodEnd)
        {
            return score with { PeriodRunning = false };
        }

        var type = ctx.Definition.EventTypes.FirstOrDefault(candidate => candidate.Code == evt.EventType);
        // The rules may change what a scoring type is worth (sport-engine.md §5, Apply), never make another type score
        // (a missed attempt is worth 0 by definition); whether a template lets tenants change it is the resolver's check.
        if (type is not { Points: { } defined and not 0 })
        {
            return score;
        }

        var points = ctx.Rules.Number(PointsRulePrefix + type.Code) is { } overridden ? checked((int)overridden) : defined;
        if (points == 0)
        {
            return score;
        }

        // Validation refuses these drafts (TIMELINE_PARTICIPANT_INVALID); a recorded one is a bug.
        if (evt.ParticipantId is not { } actor || !ctx.Sides.Any(side => side.ParticipantId == actor))
        {
            throw new InvalidOperationException($"Event {evt.Id} scores but its participant is not in the contest.");
        }

        var receiver = type.Side == ActorSide.Opponent ? ctx.OpponentOf(actor) : actor;
        var period = evt.PeriodCode is { } code ? new PlayedPeriod(code, evt.PeriodOccurrence) : null;
        var component = type.Component ?? ScoringFamilies.MainComponent;
        return score with
        {
            Participants = score.Participants.SetItem(receiver, score.For(receiver).Add(component, period, points)),
        };
    }

    /// <summary>
    /// The outcome as the score stands (sport-engine.md §5, "Outcome"; when the contest may end is Contest Operations'
    /// call): different <c>main</c> totals win; level ones follow <c>contest.tie_resolution</c> along
    /// <c>contest.extra_period_sequence</c>, or are a draw.
    /// </summary>
    public OutcomeProposal ProposeOutcome(ContestContext ctx, ScoreState state)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var score = state as AdditiveScoreState ?? throw new ArgumentException($"Expected the {FamilyCode} state.", nameof(state));
        if (ctx.Sides.Count != 2)
        {
            throw new InvalidOperationException($"The {FamilyCode} family scores head-to-head contests only.");
        }

        var (first, second) = (ctx.Sides[0].ParticipantId, ctx.Sides[1].ParticipantId);
        var played = score.Periods.Select(period => (Played: period, Definition: PhaseTracker.Period(ctx.Definition, period.PeriodCode))).ToList();
        var resolution = ctx.Rules.Text(TieResolutionRule) ?? TieResolutions.Draw;
        var difference = score.For(first).Total - score.For(second).Total;
        if (difference != 0)
        {
            var winner = difference > 0 ? first : second;
            // A score in a sudden-death period decides the contest there and then.
            if (played.Any(period => period.Definition.SuddenDeath
                && ctx.Sides.Any(side => score.For(side.ParticipantId).MainByPeriod.GetValueOrDefault(period.Played) != 0)))
            {
                return OutcomeProposal.Win(winner, OutcomeMethod.SuddenDeath) with { RequiresEnd = true };
            }

            // Which overtime method is the tie resolution's word, not the sport's: repeated periods are overtime.
            return !played.Any(period => period.Definition is { Overtime: true, Kind: PhaseKind.Play })
                ? OutcomeProposal.Win(winner, OutcomeMethod.Regulation)
                : OutcomeProposal.Win(winner, resolution == TieResolutions.RepeatExtraPeriod ? OutcomeMethod.Overtime : OutcomeMethod.ExtraTime);
        }

        if (resolution == TieResolutions.Draw)
        {
            return OutcomeProposal.Draw;
        }

        // The shoot-out is the last step: it is won on its own component, and goes on while level.
        if (played.Any(period => period.Definition.Kind == PhaseKind.Shootout))
        {
            var shootout = score.For(first).Component(ShootoutComponent) - score.For(second).Component(ShootoutComponent);
            return shootout == 0
                ? OutcomeProposal.Requires(OutcomeKind.RequiresShootout, nextPeriodCode: null)
                : OutcomeProposal.Win(shootout > 0 ? first : second, OutcomeMethod.Shootout);
        }

        return NextStep(ctx, score, played.Select(period => period.Played.PeriodCode).ToList());
    }

    // The period after the last one of the sequence started (the first if none has). Past the end, a last extra period
    // still running is what the tie needs; once it has ended, the sequence starts again at its first repeatable period
    // (overtime until decided), or the rules give no more play and level is a draw. The next period's kind, or a
    // break's next period's, says what the tie needs.
    private static OutcomeProposal NextStep(ContestContext ctx, AdditiveScoreState score, List<string> played)
    {
        List<string> sequence = [.. ctx.Rules.List(ExtraPeriodSequenceRule) ?? []];
        var last = played.Select(code => sequence.IndexOf(code)).Where(index => index >= 0).DefaultIfEmpty(-1).Last();
        var next = last + 1;
        if (next >= sequence.Count)
        {
            if (last >= 0 && score.PeriodRunning && played[^1] == sequence[last]
                && PhaseTracker.Period(ctx.Definition, sequence[last]).Kind == PhaseKind.Play)
            {
                return OutcomeProposal.Requires(OutcomeKind.RequiresExtraTime, nextPeriodCode: null);
            }

            next = Enumerable.Range(0, sequence.Count).FirstOrDefault(index => PhaseTracker.Period(ctx.Definition, sequence[index]).Repeatable, -1);
            if (next < 0)
            {
                return OutcomeProposal.Draw;
            }
        }

        var kind = sequence.Skip(next).Select(code => PhaseTracker.Period(ctx.Definition, code).Kind).FirstOrDefault(kind => kind != PhaseKind.Break, PhaseKind.Break);
        return kind switch
        {
            PhaseKind.Play => OutcomeProposal.Requires(OutcomeKind.RequiresExtraTime, sequence[next]),
            PhaseKind.Shootout => OutcomeProposal.Requires(OutcomeKind.RequiresShootout, sequence[next]),
            _ => OutcomeProposal.Draw,
        };
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
