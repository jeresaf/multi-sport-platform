using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Parameters;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

public sealed class FormatDefinitionFilesTests
{
    private static readonly FormatDefinitionFiles _files = new();

    // Changing any content of a published format without bumping its version fails here before any database (S05 D4):
    // a new version gets a new hash and a new pin. Comments and layout are not content.
    [Theory]
    [InlineData("single-contest.yaml", "SINGLE_CONTEST", "sha256:51390e0fb01f5dc7daefd2852a1e5ccb1e107a0cc99c075433ed04b10094d6f7")]
    [InlineData("round-robin.yaml", "ROUND_ROBIN", "sha256:552d61a277bc6643ce92b5fde5c6f160358acf415434fe0dadb6bb6ce602644f")]
    [InlineData("knockout.yaml", "KNOCKOUT", "sha256:442773eaa5e1b3460501ad508a2d2b0f85e19f939c3b67e90caf1a4c8b56a144")]
    public void Each_format_at_version_1_has_its_published_hash(string file, string code, string hash)
    {
        var content = YamlJson.Parse(_files.Read(file));

        var format = FormatDefinitionContent.Read(content!);
        (format.Code, format.Version).ShouldBe((code, 1));
        CanonicalJson.Hash(content).ShouldBe(hash);
    }

    [Fact]
    public void The_round_robin_reads_into_its_typed_model()
    {
        var format = FormatDefinitionContent.Read(YamlJson.Parse(_files.Read("round-robin.yaml"))!);

        format.Name.ShouldBe("League (round robin)");
        format.CompatibleShapes.ShouldBe([ContestShape.HeadToHead]);
        format.Participants.ShouldBe(new ParticipantRange(2, 40));
        format.Stages.ShouldBe([new FormatStage("LEAGUE", StageType.League, FormatGeneratorKinds.RoundRobin)]);
        format.Parameters.Keys.Order(StringComparer.Ordinal).ShouldBe(["cycles", "home_away_balance", "seeding"]);

        var cycles = format.Parameters[FormatParameters.Cycles];
        (cycles.Type, cycles.Label, cycles.Min, cycles.Max, cycles.Default).ShouldBe((ParameterType.Number, "Cycles", 1, 4, ParameterValue.Of(1)));
        cycles.Values.ShouldBeEmpty();
        format.Parameters[FormatParameters.HomeAwayBalance].Default.ShouldBe(ParameterValue.Of(true));
        var seeding = format.Parameters[FormatParameters.Seeding];
        (seeding.Type, seeding.Default).ShouldBe((ParameterType.Choice, ParameterValue.Of(Seedings.Random)));
        seeding.Values.ShouldBe(Seedings.All);
    }

    [Fact]
    public void The_knockout_and_the_single_contest_read_into_their_typed_models()
    {
        var knockout = FormatDefinitionContent.Read(YamlJson.Parse(_files.Read("knockout.yaml"))!);
        var single = FormatDefinitionContent.Read(YamlJson.Parse(_files.Read("single-contest.yaml"))!);

        knockout.Participants.ShouldBe(new ParticipantRange(2, 256));
        knockout.Parameters[FormatParameters.ThirdPlaceContest].Default.ShouldBe(ParameterValue.Of(false));
        knockout.Parameters[FormatParameters.Seeding].Default.ShouldBe(ParameterValue.Of(Seedings.Seeded));
        single.Participants.ShouldBe(new ParticipantRange(2, 2));
        single.Stages.ShouldHaveSingleItem().Type.ShouldBe(StageType.Single);
        single.Parameters.ShouldBeEmpty();
    }

    // The model refuses a key it doesn't map, so a schema change without a model change fails loudly.
    [Fact]
    public void The_model_refuses_an_unmapped_key()
    {
        var content = YamlJson.Parse(_files.Read("knockout.yaml"))!.AsObject();
        content["rounds"] = 3;

        Should.Throw<System.Text.Json.JsonException>(() => FormatDefinitionContent.Read(content));
    }

    [Theory]
    [InlineData("2", ParameterType.Number, "2")]
    [InlineData("true", ParameterType.Flag, "true")]
    [InlineData("\"RANDOM\"", ParameterType.Choice, "RANDOM")]
    public void A_parameter_value_is_its_bare_JSON_value(string json, ParameterType type, string text)
    {
        var value = System.Text.Json.JsonSerializer.Deserialize<ParameterValue>(json)!;

        (value.Type, value.ToString()).ShouldBe((type, text));
        System.Text.Json.JsonSerializer.Serialize(value).ShouldBe(json);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("\"\"")]
    [InlineData("{}")]
    public void Anything_else_is_not_a_parameter_value(string json)
    {
        Should.Throw<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<ParameterValue>(json));
    }
}
