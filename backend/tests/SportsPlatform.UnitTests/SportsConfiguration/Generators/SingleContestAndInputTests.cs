using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.SportsConfiguration;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats.Generation;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using static SportsPlatform.UnitTests.SportsConfiguration.Generators.GeneratorTestData;

namespace SportsPlatform.UnitTests.SportsConfiguration.Generators;

// The single contest, and what every generator refuses (S05 D10 and the T3b plan's answers).
public sealed class SingleContestAndInputTests
{
    [Fact]
    public void A_single_contest_is_one_round_with_the_first_entrant_at_home()
    {
        var contest = Generate(SingleContest, Input(["NTINDA_SWIFTS", "KISAASI_KESTRELS"]));

        var stage = contest.Stages.ShouldHaveSingleItem();
        (stage.Code, stage.Type).ShouldBe(("MAIN", StageType.Single));
        var round = stage.Rounds.ShouldHaveSingleItem();
        (round.Number, round.Code, round.BracketPlaces).ShouldBe((1, "ROUND_1", (int?)null));
        round.Pairings.ShouldBe([new GeneratedPairing("ROUND_1_1", PairingSide.Entrant("NTINDA_SWIFTS"), PairingSide.Entrant("KISAASI_KESTRELS"))]);
        contest.Parameters.ShouldBeEmpty();
        (contest.FormatCode, contest.FormatVersion).ShouldBe(("SINGLE_CONTEST", 1));
        contest.OutputHash.ShouldStartWith("sha256:");
    }

    [Theory]
    [InlineData("SINGLE_CONTEST", 3)]
    [InlineData("ROUND_ROBIN", 1)]
    [InlineData("ROUND_ROBIN", 41)]
    [InlineData("KNOCKOUT", 1)]
    [InlineData("KNOCKOUT", 257)]
    public void A_count_outside_the_format_is_refused(string code, int count)
    {
        var error = Refusal(Format(code), Input(Entrants(count)));

        error.Code.ShouldBe(SportsConfigurationErrors.FormatParticipantCountInvalid.Code);
        error.Message.ShouldContain($"not {count}");
    }

    [Fact]
    public void Parameters_left_out_take_their_defaults_and_bad_ones_are_refused_by_name()
    {
        Generate(Knockout, Input(Entrants(4))).Parameters.ShouldBe(new Dictionary<string, ParameterValue>
        {
            [FormatParameters.Seeding] = ParameterValue.Of(Seedings.Seeded),
            [FormatParameters.ThirdPlaceContest] = ParameterValue.Of(false),
        });

        (string Code, ParameterValue Value)[][] refused =
        [
            [("best_of", ParameterValue.Of(3))],
            [(FormatParameters.Cycles, ParameterValue.Of(5))],
            [(FormatParameters.Cycles, ParameterValue.Of(0))],
            [(FormatParameters.Cycles, ParameterValue.Of(true))],
            [(FormatParameters.Seeding, ParameterValue.Of("SNAKE"))],
            [(FormatParameters.HomeAwayBalance, ParameterValue.Of("YES"))],
        ];
        foreach (var parameters in refused)
        {
            var error = Refusal(RoundRobin, Input(Entrants(6), parameters: parameters));
            error.Code.ShouldBe(SportsConfigurationErrors.FormatParameterInvalid.Code);
            error.Message.ShouldContain(parameters[0].Code);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void A_third_place_contest_needs_four_participants(int count)
    {
        var error = Refusal(Knockout, Input(Entrants(count), parameters: (FormatParameters.ThirdPlaceContest, ParameterValue.Of(true))));

        error.Code.ShouldBe(SportsConfigurationErrors.FormatParameterInvalid.Code);
        error.Message.ShouldContain(FormatParameters.ThirdPlaceContest);
    }

    public static TheoryData<string, string?[], string> BadEntrants() => new()
    {
        { "ROUND_ROBIN", ["BUKOTO_COMETS", "NAKAWA_NOMADS", "BUKOTO_COMETS"], "given twice" },
        { "ROUND_ROBIN", ["BUKOTO_COMETS", " "], "blank" },
        { "ROUND_ROBIN", ["BUKOTO_COMETS", "NAKAWA_NOMADS "], "untrimmed" },
        { "ROUND_ROBIN", ["BUKOTO_COMETS", null, "NAKAWA_NOMADS"], "bye" },
        { "KNOCKOUT", ["BUKOTO_COMETS", null, "NAKAWA_NOMADS", "SEETA_SPARROWS"], "bye" },
    };

    [Theory]
    [MemberData(nameof(BadEntrants))]
    public void Blank_repeated_or_misplaced_entrants_are_refused(string code, string?[] entrants, string problem)
    {
        var error = Refusal(Format(code), Input(entrants));

        error.Code.ShouldBe(SportsConfigurationErrors.FormatEntrantsInvalid.Code);
        error.Message.ShouldContain(problem);
    }

    [Theory]
    [InlineData(new[] { "BUKOTO_COMETS", null, null, null, "NAKAWA_NOMADS", "SEETA_SPARROWS", "BUNGA_BREAKERS", "KYEBANDO_RAVENS" }, "both byes")]
    [InlineData(new[] { "BUKOTO_COMETS", null, "NAKAWA_NOMADS", "SEETA_SPARROWS", "BUNGA_BREAKERS", null }, "4 participants need 4 bracket lines, not 6")]
    [InlineData(new[] { "BUKOTO_COMETS", null, "NAKAWA_NOMADS", null, null, null, null, null }, "2 participants need 2 bracket lines, not 8")]
    public void Manual_bracket_lines_of_the_wrong_length_or_with_two_byes_together_are_refused(string?[] lines, string problem)
    {
        var error = Refusal(Knockout, Input(lines, parameters: (FormatParameters.Seeding, ParameterValue.Of(Seedings.Manual))));

        error.Code.ShouldBe(SportsConfigurationErrors.FormatEntrantsInvalid.Code);
        error.Message.ShouldContain(problem);
    }

    [Fact]
    public void The_hash_covers_the_inputs_as_well_as_the_structure()
    {
        // SEEDED ignores the seed and keeps the structure, but the draw is another one.
        FormatGenerationInput Draw(ulong seed) => Input(Clubs.Take(4).ToList(), seed, (FormatParameters.Seeding, ParameterValue.Of(Seedings.Seeded)));

        var one = Generate(RoundRobin, Draw(1));
        var other = Generate(RoundRobin, Draw(2));

        other.Stages[0].Rounds.SelectMany(round => round.Pairings).ShouldBe(one.Stages[0].Rounds.SelectMany(round => round.Pairings));
        other.OutputHash.ShouldNotBe(one.OutputHash);
    }

    private static FormatDefinition Format(string code) => code switch
    {
        "SINGLE_CONTEST" => SingleContest,
        "ROUND_ROBIN" => RoundRobin,
        _ => Knockout,
    };

    private static Error Refusal(FormatDefinition format, FormatGenerationInput input)
    {
        var result = Generator.Generate(format, input);
        result.IsFailure.ShouldBeTrue();
        return result.Error;
    }
}
