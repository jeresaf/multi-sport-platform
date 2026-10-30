using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// docs/domain/sports holds the reference files; the backend embeds its own copies (docs/domain/sports/README.md).
public sealed class ReferenceSportFilesTests
{
    private static readonly string _docs = Path.Combine(RepositoryRoot(), "docs", "domain", "sports");

    private static readonly SportDefinitionFiles _files = new();

    [Fact]
    public void Every_backend_definition_equals_its_docs_copy()
    {
        _files.Names.ShouldNotBeEmpty();
        foreach (var file in _files.Names)
        {
            _files.Read(file).ShouldBe(File.ReadAllText(Path.Combine(_docs, file)), $"{file} differs from docs/domain/sports/{file}: update both");
        }
    }

    // Not seeded until S15–S16, but the schema and the rules are written for every ADDITIVE reference file, so a rule
    // too narrow for them fails now rather than when their slice starts. Cricket (INNINGS) has no engine until S17.
    [Theory]
    [InlineData("rugby-union-15.yaml")]
    [InlineData("rugby-union-7.yaml")]
    [InlineData("basketball.yaml")]
    public void The_other_ADDITIVE_reference_files_are_valid(string file)
    {
        SportDefinitionValidator.Validate(file, File.ReadAllText(Path.Combine(_docs, file))).ShouldBeEmpty();
    }

    // The typed model maps every key the schema allows, so each reference file reads without an unmapped member.
    [Theory]
    [InlineData("football.yaml")]
    [InlineData("rugby-union-15.yaml")]
    [InlineData("rugby-union-7.yaml")]
    [InlineData("basketball.yaml")]
    public void The_typed_model_reads_every_ADDITIVE_reference_file(string file)
    {
        var definition = SportDefinitionContent.Read(YamlJson.Parse(File.ReadAllText(Path.Combine(_docs, file)))!);

        definition.SchemaVersion.ShouldBe(1);
        definition.ScoringFamily.ShouldBe("ADDITIVE");
        definition.EventTypes.ShouldNotBeEmpty();
        definition.Periods.ShouldNotBeEmpty();
    }

    [Fact]
    public void The_typed_model_keeps_the_file_values()
    {
        var definition = SportDefinitionContent.Read(YamlJson.Parse(_files.Read("football.yaml"))!);

        definition.Shape.ShouldBe(ContestShape.HeadToHead);
        definition.Sides.ShouldBe(["HOME", "AWAY"]);
        definition.CoverageLevels.ShouldBe([CoverageLevel.ResultOnly, CoverageLevel.Basic, CoverageLevel.Standard, CoverageLevel.Detailed]);
        definition.Terminology.Contest.ShouldBe("Match");
        definition.Periods[0].ShouldBe(new PeriodDefinition("FIRST_HALF", "1st half", "1H", PhaseKind.Play, PeriodClock.CountUp)
        {
            DefaultSeconds = 2700,
            ClockStartsAtSeconds = 0,
            AllowsAddedTime = true,
        });
        definition.ActorRoles["OFFENDER"].ShouldBe(new ActorRoleDefinition(ActorPool.SquadOrStaff, ActorSide.Own));
        var goal = definition.EventTypes[0];
        (goal.Code, goal.Points, goal.Component, goal.Side).ShouldBe(("GOAL", (int?)1, "main", (ActorSide?)ActorSide.Own));
        goal.Actors.Select(actor => (actor.Role, actor.RequiredFrom, actor.Optional, actor.DistinctFrom))
            .ShouldBe([("SCORER", CoverageLevel.Standard, false, null), ("ASSIST", null, true, "SCORER")]);
        definition.EventTypes.SelectMany(type => type.Limits).Select(limit => limit.Rule).ShouldContain("substitutions.max");
        definition.Statistics.Single(statistic => statistic.Code == "SHOTS_ON_TARGET").Derive.Where
            .ShouldBe(new Dictionary<string, string> { ["on_target"] = "true" });
        definition.Statistics.Single(statistic => statistic.Code == "POSSESSION_PERCENT").Derive.Manual.ShouldBeTrue();
    }

    // Changing any content of the published football@1 without bumping its version fails here before any database
    // (S05 D4): a new version gets a new hash and a new pin. Comments and layout are not content.
    [Fact]
    public void Football_version_1_has_its_published_hash()
    {
        var content = YamlJson.Parse(_files.Read("football.yaml"));

        SportDefinitionContent.Read(content!).Version.ShouldBe(1);
        CanonicalJson.Hash(content).ShouldBe(_footballVersion1Hash);
    }

    private const string _footballVersion1Hash = "sha256:2884e551a7d6855359e4bb7e7599e7308611896947f902370746ddb4ce10918e";

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "SportsPlatform.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("backend/SportsPlatform.slnx not found above the test output folder.");
    }
}
