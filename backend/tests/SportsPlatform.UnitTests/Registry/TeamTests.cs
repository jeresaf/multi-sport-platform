using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.Teams;
using SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// Teams (S07-T3a): the record's keys, name's start and first history row (D6, D8, D13), the colours' order, the slug
/// proposed from the name and category (D9; owner, T3a plan), the warning's input, the places the quota counts (D14,
/// Q50) and the conflict code (D2).
/// </summary>
public sealed class TeamTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    private static readonly TeamCategory _seniorMen = new(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First);

    private static TeamDetails NtindaSwifts() =>
        new("Ntinda Swifts FC", "Ntinda Swifts", _seniorMen, null, "NTS", "Swift and sure", "#0A3D91", "#FFFFFF", null, null, 1998);

    [Fact]
    public void A_new_team_is_active_with_its_keys_its_names_start_and_the_first_row_of_its_history()
    {
        var team = Team.Create("TESTBALL_FULL", NtindaSwifts(), new DateOnly(2020, 1, 1), "ntinda-swifts-fc-senior-men", TeamSource.Manual, SystemActor.Instance, _now);

        (team.Status, team.Variant, team.Slug, team.NameSince).ShouldBe((TeamStatus.Active, "TESTBALL_FULL", "ntinda-swifts-fc-senior-men", new DateOnly(2020, 1, 1)));
        (team.NameKey, team.ShortNameKey, team.SortKey).ShouldBe(("fc ntinda swifts", "ntinda swifts", "ntinda swifts fc"));
        team.Category.ShouldBe(_seniorMen);
        (team.LogoAssetId, team.BadgeAssetId).ShouldBe((null, null));
        var first = team.StatusHistory.ShouldHaveSingleItem();
        (first.TeamId, first.FromStatus, first.ToStatus, first.Reason, first.OccurredAt).ShouldBe((team.Id, null, TeamStatus.Active, null, _now));
    }

    [Fact]
    public void A_team_refuses_a_name_without_letters_colours_out_of_order_a_bad_slug_and_early_dates()
    {
        Team Create(TeamDetails details, string slug = "ntinda", DateOnly? nameSince = null) =>
            Team.Create("TESTBALL_FULL", details, nameSince ?? new DateOnly(2020, 1, 1), slug, TeamSource.Manual, SystemActor.Instance, _now);

        Should.Throw<ArgumentException>(() => Create(NtindaSwifts() with { Name = "--" }));
        Should.Throw<ArgumentException>(() => Create(NtindaSwifts() with { PrimaryColour = null }));
        Should.Throw<ArgumentException>(() => Create(NtindaSwifts() with { SecondaryColour = null, TertiaryColour = "#F2C200" }));
        Should.Throw<ArgumentException>(() => Create(NtindaSwifts(), slug: "Ntinda Swifts"));
        Should.Throw<ArgumentOutOfRangeException>(() => Create(NtindaSwifts() with { FoundedYear = 1799 }));
        Should.Throw<ArgumentOutOfRangeException>(() => Create(NtindaSwifts(), nameSince: new DateOnly(1799, 12, 31)));
    }

    // D6: a secondary colour needs a primary, a tertiary a secondary.
    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData("#0A3D91", null, null, true)]
    [InlineData("#0A3D91", "#FFFFFF", null, true)]
    [InlineData("#0A3D91", "#FFFFFF", "#F2C200", true)]
    [InlineData(null, "#FFFFFF", null, false)]
    [InlineData("#0A3D91", null, "#F2C200", false)]
    [InlineData(null, null, "#F2C200", false)]
    public void Colours_come_in_order(string? primary, string? secondary, string? tertiary, bool inOrder) =>
        TeamDetails.ColoursInOrder(primary, secondary, tertiary).ShouldBe(inOrder);

    // D9 (owner, S07-T3a plan): the name, the age group and gender codes, and the level unless it is FIRST.
    [Theory]
    [InlineData("Ntinda Swifts FC", GenderCategory.Men, AgeGroup.Senior, TeamLevel.First, "ntinda-swifts-fc-senior-men")]
    [InlineData("Seeta Sparrows FC", GenderCategory.Men, AgeGroup.U17, TeamLevel.First, "seeta-sparrows-fc-u17-men")]
    [InlineData("Kireka Kingfishers FC", GenderCategory.Men, AgeGroup.Senior, TeamLevel.Reserve, "kireka-kingfishers-fc-senior-men-reserve")]
    [InlineData("Lakeshore High School", GenderCategory.Women, AgeGroup.U16, TeamLevel.First, "lakeshore-high-school-u16-women")]
    [InlineData("Kololo Veterans", GenderCategory.Mixed, AgeGroup.Veterans, TeamLevel.Academy, "kololo-veterans-veterans-mixed-academy")]
    [InlineData("Ndejje Ñandú", GenderCategory.Women, AgeGroup.Senior, TeamLevel.Development, "ndejje-nandu-senior-women-development")]
    [InlineData("***", GenderCategory.Men, AgeGroup.Senior, TeamLevel.First, "senior-men")]
    public void A_slug_is_proposed_from_the_name_and_category(string name, GenderCategory gender, AgeGroup ageGroup, TeamLevel level, string slug) =>
        Team.ProposeSlug(name, new TeamCategory(gender, ageGroup, level)).ShouldBe(slug);

    [Fact]
    public void The_warning_compares_the_name_and_short_name_keys_the_variant_category_and_organisation()
    {
        var club = Guid.CreateVersion7();
        var input = CreateTeamHandler.InputOf("TESTBALL_FULL", NtindaSwifts() with { ShortName = "FC Ntinda Swifts", OrganisationId = club }, teamId: null);

        input.ShouldBe(new TeamDuplicateInput("fc ntinda swifts", "fc ntinda swifts", "TESTBALL_FULL", _seniorMen, club, null));
        input.NameKeys.ShouldBe(["fc ntinda swifts"]);
        CreateTeamHandler.InputOf("TESTBALL_FULL", NtindaSwifts() with { ShortName = null }, null).ShortNameKey.ShouldBeNull();
    }

    // D14 (owner, Q50): active, inactive and suspended teams count; disbanded and archived ones don't.
    [Theory]
    [InlineData(TeamStatus.Active, true)]
    [InlineData(TeamStatus.Inactive, true)]
    [InlineData(TeamStatus.Suspended, true)]
    [InlineData(TeamStatus.Disbanded, false)]
    [InlineData(TeamStatus.Archived, false)]
    public void Teams_that_take_a_place(TeamStatus status, bool counts) => TeamCount.Counts(status).ShouldBe(counts);

    [Fact]
    public void A_conflict_on_a_team_is_the_teams()
    {
        RegistryConcurrencyConflicts.For(typeof(Team))!.Code.ShouldBe("TEAM_CONCURRENCY_CONFLICT");
        RegistryConcurrencyConflicts.For(typeof(TeamStatusChange)).ShouldBeNull();
    }
}
