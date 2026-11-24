using System.Buffers.Text;
using System.Text;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.Teams;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// The teams' order (S07-T3b-2; owner, plan 2026-11-24): category by age group in declared order, then gender, then
/// level; the rank expression (compiled here; run through PostgreSQL in the integration tests) computes the C# rank for
/// every category; the directory's cursor carries a real rank.
/// </summary>
public sealed class TeamOrderTests
{
    private static readonly Guid _id = new("0192a1b2-0000-7000-8000-00000000e001");

    private static IEnumerable<TeamCategory> AllCategories() =>
        from ageGroup in Enum.GetValues<AgeGroup>()
        from gender in Enum.GetValues<GenderCategory>()
        from level in Enum.GetValues<TeamLevel>()
        select new TeamCategory(gender, ageGroup, level);

    [Fact]
    public void Categories_rank_by_age_group_then_gender_then_level()
    {
        var ordered = new[]
        {
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Reserve),
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Development),
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Academy),
            new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First),
            new TeamCategory(GenderCategory.Mixed, AgeGroup.Senior, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.U6, TeamLevel.First),
            new TeamCategory(GenderCategory.Women, AgeGroup.U10, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.U17, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.U23, TeamLevel.Academy),
            new TeamCategory(GenderCategory.Men, AgeGroup.Veterans, TeamLevel.First),
        };

        ordered.Select(TeamOrder.CategoryRankOf).ShouldBeInOrder(SortDirection.Ascending);
        ordered.Select(TeamOrder.CategoryRankOf).Distinct().Count().ShouldBe(ordered.Length);
    }

    [Fact]
    public void Every_category_has_its_own_rank_and_the_expression_computes_the_same_one()
    {
        var rankOf = TeamOrder.CategoryRank.Compile();
        var ranks = new HashSet<int>();
        foreach (var category in AllCategories())
        {
            var team = Team.Create(
                "TESTBALL_FULL", new TeamDetails("Ntinda Swifts FC", null, category, null, null, null, null, null, null, null, null),
                new DateOnly(2020, 1, 1), "ntinda-swifts-fc", TeamSource.Manual, SystemActor.Instance, DateTimeOffset.UnixEpoch);

            var rank = TeamOrder.CategoryRankOf(category);
            rankOf(team).ShouldBe(rank, category.ToString());
            TeamOrder.IsCategoryRank(rank).ShouldBeTrue();
            ranks.Add(rank).ShouldBeTrue(category.ToString());
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3 + 1)] // a level past ACADEMY
    [InlineData(30)] // a gender past MIXED
    [InlineData(2000)] // an age group past VETERANS
    public void A_number_that_is_no_categorys_rank_is_refused(int rank) => TeamOrder.IsCategoryRank(rank).ShouldBeFalse();

    [Theory]
    [InlineData("ntinda swifts fc", 0)]
    [InlineData("ntinda swifts fc", 1210)]
    [InlineData("nsambya sportsplätz", 1923)]
    public void A_cursor_survives_encoding(string sortKey, int rank)
    {
        var cursor = new TeamDirectoryCursor(sortKey, rank, _id);

        var text = cursor.Encode();

        text.ShouldMatch("^[A-Za-z0-9_-]+$");
        TeamDirectoryCursor.TryDecode(text, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    [Fact]
    public void A_cursor_of_the_longest_name_and_rank_survives_encoding()
    {
        var cursor = new TeamDirectoryCursor(new string('አ', TeamDetails.NameMaxLength), 1923, _id);

        TeamDirectoryCursor.TryDecode(cursor.Encode(), out var decoded).ShouldBeTrue();
        decoded.ShouldBe(cursor);
    }

    [Theory]
    [InlineData("ntinda swifts fc\n0192a1b200007000800000000000e001")] // a directory cursor: no rank
    [InlineData("ntinda swifts fc\n10\nnot-an-id")]
    [InlineData("ntinda swifts fc\nten\n0192a1b200007000800000000000e001")]
    [InlineData("ntinda swifts fc\n30\n0192a1b200007000800000000000e001")] // no category's rank
    [InlineData("ntinda swifts fc\n-1\n0192a1b200007000800000000000e001")]
    [InlineData("ntinda swifts fc\n010\n0192a1b200007000800000000000e001")] // not as encoded
    [InlineData("\n10\n0192a1b200007000800000000000e001")] // no sort key
    [InlineData("ntinda swifts fc\n10\n0192a1b200007000800000000000e001\nmore")]
    public void A_cursor_with_other_content_is_refused(string content) =>
        TeamDirectoryCursor.TryDecode(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(content)), out _).ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Text_that_is_not_a_cursor_is_refused(string? text) => TeamDirectoryCursor.TryDecode(text, out _).ShouldBeFalse();

    [Fact]
    public void A_cursor_longer_than_a_teams_name_is_refused() =>
        TeamDirectoryCursor.TryDecode(new TeamDirectoryCursor(new string('a', TeamDetails.NameMaxLength + 1), 0, _id).Encode(), out _)
            .ShouldBeFalse();
}
