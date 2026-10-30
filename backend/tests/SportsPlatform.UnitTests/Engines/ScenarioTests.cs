using System.Text.Json;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;
using SportsPlatform.Modules.SportsConfiguration.Engines;

namespace SportsPlatform.UnitTests.Engines;

// S05 D9: each scenario is recorded event by event as a scorer would — validate against the live state, then append —
// and the folded result is compared with what the file expects.
public sealed class ScenarioTests
{
    private static readonly Guid _names = Guid.Parse("6d1f0c2e-5a8b-4f3e-9c71-2b0e8d4a6f15");

    private static readonly ScoringEngines _engines = new([new AdditiveEngine()]);

    public static TheoryData<string> Files() =>
        [.. Directory.EnumerateFiles(Scenario.Folder, "*.yaml", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) != "rules")
            .Select(path => Path.GetRelativePath(Scenario.Folder, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void There_are_football_scenarios()
    {
        Files().Select(row => row.Data).ShouldContain(file => file.StartsWith("football/", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Scenario_plays_out_as_written(string file)
    {
        var scenario = Scenario.Read(Path.Combine(Scenario.Folder, file));
        var teams = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            [scenario.Home.Team] = Name(scenario.Home.Team),
            [scenario.Away.Team] = Name(scenario.Away.Team),
        };
        var ctx = new ContestContext(
            scenario.SportDefinition(),
            scenario.ContestRules(),
            [new ContestSide("HOME", teams[scenario.Home.Team]), new ContestSide("AWAY", teams[scenario.Away.Team])],
            scenario.Coverage)
        {
            Lineups = [Lineup(teams[scenario.Home.Team], scenario.Home), Lineup(teams[scenario.Away.Team], scenario.Away)],
        };
        var engine = _engines.EngineFor(ctx.Definition);
        var timeline = new List<TimelineEvent>();
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var (step, index) in scenario.Events.Select((step, index) => (step, index)))
        {
            var where = $"{file}: {step.Describe(index)}";
            var draft = Draft(step, teams, ids, where);
            var live = _engines.Fold(ctx, scenario.Status, timeline);
            var errors = engine.Validate(ctx, live, draft).Errors.Select(error => error.Code).ToList();
            errors.ShouldBe(step.Rejected, where);
            if (errors.Count > 0)
            {
                continue;
            }

            var recorded = Record(draft, timeline.Count + 1, live.Phase);
            timeline.Add(recorded);
            if (step.Id is { } label)
            {
                ids.Add(label, recorded.Id);
            }
        }

        var final = _engines.Fold(ctx, scenario.Status, timeline);
        var summary = engine.Summarize(ctx, final.Score);
        foreach (var (team, expected) in scenario.Expect.Score)
        {
            teams.ShouldContainKey(team, $"{file}: unknown team in expect.score");
            var score = summary.Participants.Single(participant => participant.ParticipantId == teams[team]);
            score.Primary.ShouldBe(Text(expected.Total), $"{file}: {team}'s total");
            score.Secondary.ShouldBe(expected.Shootout is { } shootout ? Text(shootout) : null, $"{file}: {team}'s shoot-out");
            if (expected.Periods.Count > 0)
            {
                score.Periods.ToDictionary(period => period.ShortLabel, period => period.Value)
                    .ShouldBe(expected.Periods.ToDictionary(period => period.Key, period => Text(period.Value)), $"{file}: {team}'s periods");
            }
        }

        var outcome = engine.ProposeOutcome(ctx, final.Score);
        var expectedOutcome = scenario.Expect.Outcome;
        if (expectedOutcome.Draw)
        {
            outcome.Kind.ShouldBe(OutcomeKind.Draw, $"{file}: outcome");
        }
        else
        {
            (outcome.Kind, outcome.WinnerParticipantId, outcome.Method)
                .ShouldBe((OutcomeKind.Winner, (Guid?)teams[expectedOutcome.Winner!], expectedOutcome.Method), $"{file}: outcome");
        }
    }

    private static TimelineEventDraft Draft(ScenarioEvent step, Dictionary<string, Guid> teams, Dictionary<string, Guid> ids, string where)
    {
        var (clock, added) = step.Clock();
        return new TimelineEventDraft(step.Type, TimelineSource.Live)
        {
            ParticipantId = step.Team is { } team
                ? teams.TryGetValue(team, out var participant) ? participant : throw new InvalidOperationException($"{where}: \"{team}\" is not one of the two teams.")
                : null,
            PeriodCode = step.Period,
            GameClockSeconds = clock,
            AddedTimeSeconds = added,
            Actors = step.Actors.ToDictionary(actor => actor.Key, actor => Name(actor.Value)),
            Payload = step.Payload.ToDictionary(field => field.Key, field => Value(field.Value)),
            SupersedesEventId = step.Supersedes is { } supersedes ? Reference(ids, supersedes, where) : null,
            ReversesEventId = step.Reverses is { } reverses ? Reference(ids, reverses, where) : null,
        };
    }

    // What Match Operations will do from S12: an id, the next sequence, and the period from the live phase (a start
    // opens the next occurrence of the period it names).
    private static TimelineEvent Record(TimelineEventDraft draft, long sequence, ContestPhase phase)
    {
        var (period, occurrence) = draft.EventType == GenericEventCodes.PeriodStart
            ? (draft.PeriodCode, phase.Played.Count(played => played.PeriodCode == draft.PeriodCode) + 1)
            : (phase.PeriodCode, phase.Occurrence);
        return new TimelineEvent(EngineTestData.Id(sequence), sequence, draft.EventType, draft.Source)
        {
            ParticipantId = draft.ParticipantId,
            PeriodCode = period,
            PeriodOccurrence = occurrence,
            GameClockSeconds = draft.GameClockSeconds,
            AddedTimeSeconds = draft.AddedTimeSeconds,
            Actors = draft.Actors,
            Payload = draft.Payload,
            SupersedesEventId = draft.SupersedesEventId,
            ReversesEventId = draft.ReversesEventId,
        };
    }

    private static ContestLineup Lineup(Guid participant, ScenarioTeam team) =>
        new(participant, [.. team.Starters.Select(Name)], [.. team.Bench.Select(Name)]) { Staff = [.. team.Staff.Select(Name)] };

    private static Guid Reference(Dictionary<string, Guid> ids, string label, string where) =>
        ids.TryGetValue(label, out var id) ? id : throw new InvalidOperationException($"{where}: no earlier event has id \"{label}\".");

    private static Guid Name(string name) => DeterministicIds.Create(_names, name);

    private static string Value(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();

    private static string Text(int points) => points.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
