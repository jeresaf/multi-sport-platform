using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.Teams;
using SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;
using SportsPlatform.Modules.Registry.Infrastructure;
using Contracts = SportsPlatform.Modules.Registry.Contracts.Teams;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// Teams (S07-T3a, T3b-1): the record's keys, name's start and first history row (D6, D8, D13), the colours' order, the
/// slug proposed from the name and category (D9; owner, T3a plan), the warning's input, the places the quota counts (D14,
/// Q50), the conflict code (D2), the update that keeps the name (D8, D11), the lifecycle (D12, Q55), and (T3d) the
/// identifier's codes and the directory's mappings (D10, D18).
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

    // Review #127 R2: a long name is cut, never the category, so a club's two categories of one long name get two slugs.
    [Fact]
    public void A_long_name_is_cut_so_the_category_codes_always_fit()
    {
        const string name = "Kawempe Northern Division Community Development Association Football Club of the Lakeside Parishes";
        var underSeventeen = Team.ProposeSlug(name, new TeamCategory(GenderCategory.Men, AgeGroup.U17, TeamLevel.First));
        var reserveWomen = Team.ProposeSlug(name, new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.Reserve));

        underSeventeen.ShouldStartWith("kawempe-northern-division-community");
        underSeventeen.ShouldEndWith("-u17-men");
        reserveWomen.ShouldEndWith("-senior-women-reserve");
        underSeventeen.ShouldNotBe(reserveWomen);
        foreach (var slug in new[] { underSeventeen, reserveWomen })
        {
            slug.Length.ShouldBeLessThanOrEqualTo(Slugs.MaxLength - 7);
            Slugs.IsValid(slug).ShouldBeTrue();
            slug.ShouldNotContain("--");
        }
    }

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

    // D12's team table: every pair from a status that isn't archived.
    private static readonly HashSet<(TeamStatus, TeamStatus)> _allowed =
    [
        .. new[] { TeamStatus.Active, TeamStatus.Inactive, TeamStatus.Suspended }.SelectMany(from =>
            Enum.GetValues<TeamStatus>().Where(to => to != from).Select(to => (from, to))),
        (TeamStatus.Disbanded, TeamStatus.Active),
        (TeamStatus.Disbanded, TeamStatus.Archived),
    ];

    public static TheoryData<TeamStatus, TeamStatus, bool> Moves()
    {
        var data = new TheoryData<TeamStatus, TeamStatus, bool>();
        foreach (var from in Enum.GetValues<TeamStatus>())
        {
            foreach (var to in Enum.GetValues<TeamStatus>())
            {
                data.Add(from, to, _allowed.Contains((from, to)));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Moves))]
    public void The_lifecycle_allows_only_its_moves(TeamStatus from, TeamStatus to, bool allowed) =>
        TeamTransitions.IsAllowed(from, to).ShouldBe(allowed);

    [Fact]
    public void The_moves_are_the_specs_table()
    {
        // Spelled out once, so the generated table above can't drift from D12 unnoticed.
        TeamTransitions.IsAllowed(TeamStatus.Disbanded, TeamStatus.Active).ShouldBeTrue();
        TeamTransitions.IsAllowed(TeamStatus.Disbanded, TeamStatus.Inactive).ShouldBeFalse();
        TeamTransitions.IsAllowed(TeamStatus.Disbanded, TeamStatus.Suspended).ShouldBeFalse();
        TeamTransitions.IsAllowed(TeamStatus.Suspended, TeamStatus.Disbanded).ShouldBeTrue();
        TeamTransitions.IsAllowed(TeamStatus.Inactive, TeamStatus.Archived).ShouldBeTrue();
    }

    // Q55: unarchiving goes back to the status before archiving and nowhere else.
    [Theory]
    [InlineData(TeamStatus.Active)]
    [InlineData(TeamStatus.Inactive)]
    [InlineData(TeamStatus.Suspended)]
    [InlineData(TeamStatus.Disbanded)]
    public void An_archived_team_goes_back_only_to_the_status_it_had(TeamStatus before)
    {
        foreach (var to in Enum.GetValues<TeamStatus>())
        {
            TeamTransitions.IsAllowed(TeamStatus.Archived, to, before).ShouldBe(to == before, $"{before} → ARCHIVED → {to}");
        }

        TeamTransitions.IsAllowed(TeamStatus.Archived, TeamStatus.Active).ShouldBeFalse();
    }

    [Fact]
    public void A_disbanded_team_archived_and_unarchived_stays_disbanded_and_each_move_is_kept()
    {
        var team = NewTeam();

        team.ChangeStatus(TeamStatus.Disbanded, " Not enough players ", SystemActor.Instance, _now.AddMinutes(1)).IsSuccess.ShouldBeTrue();
        team.ChangeStatus(TeamStatus.Archived, "Folded into the first team", SystemActor.Instance, _now.AddMinutes(2)).IsSuccess.ShouldBeTrue();
        team.StatusBeforeArchive().ShouldBe(TeamStatus.Disbanded);
        team.ChangeStatus(TeamStatus.Active, "Archived by mistake", SystemActor.Instance, _now.AddMinutes(3))
            .Error!.Code.ShouldBe("TEAM_STATUS_INVALID");
        team.ChangeStatus(TeamStatus.Disbanded, "Archived by mistake", SystemActor.Instance, _now.AddMinutes(3)).IsSuccess.ShouldBeTrue();

        team.Status.ShouldBe(TeamStatus.Disbanded);
        team.StatusBeforeArchive().ShouldBeNull();
        team.StatusHistory.Select(c => (c.FromStatus, c.ToStatus, c.Reason)).ShouldBe(
        [
            (null, TeamStatus.Active, null),
            (TeamStatus.Active, TeamStatus.Disbanded, "Not enough players"),
            (TeamStatus.Disbanded, TeamStatus.Archived, "Folded into the first team"),
            (TeamStatus.Archived, TeamStatus.Disbanded, "Archived by mistake"),
        ]);
    }

    [Fact]
    public void A_move_needs_the_history_loaded()
    {
        // As EF materialises a team without its history: no rows at all.
        var team = (Team)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Team));
        typeof(Team).GetField("_statusHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(team, new List<TeamStatusChange>());

        Should.Throw<InvalidOperationException>(() => team.ChangeStatus(TeamStatus.Inactive, "No fixtures", SystemActor.Instance, _now));
    }

    [Fact]
    public void An_update_keeps_the_name_and_variant_and_says_whether_a_compared_field_changed()
    {
        var team = NewTeam();
        var same = NtindaSwifts() with { Motto = "By the lake", Code = "NSW" };

        team.ChangesComparedFields(same).ShouldBeFalse();
        team.ChangesComparedFields(same with { ShortName = "  ntinda SWIFTS " }).ShouldBeFalse();
        team.ChangesComparedFields(same with { ShortName = "Swifts" }).ShouldBeTrue();
        team.ChangesComparedFields(same with { ShortName = null }).ShouldBeTrue();
        team.ChangesComparedFields(same with { Category = _seniorMen with { Level = TeamLevel.Reserve } }).ShouldBeTrue();
        team.ChangesComparedFields(same with { OrganisationId = Guid.CreateVersion7() }).ShouldBeTrue();

        team.Update(same with { ShortName = "Swifts" }, "ntinda-swifts");
        (team.Name, team.NameKey, team.Variant, team.Slug, team.ShortName, team.ShortNameKey, team.Code, team.Motto)
            .ShouldBe(("Ntinda Swifts FC", "fc ntinda swifts", "TESTBALL_FULL", "ntinda-swifts", "Swifts", "swifts", "NSW", "By the lake"));
        Should.Throw<ArgumentException>(() => team.Update(same with { Name = "Bunga Lakeside FC" }, "ntinda-swifts"));
        Should.Throw<ArgumentException>(() => team.Update(same, "Ntinda Swifts"));
    }

    // S07 D10 (T3d): ending a team's identifier answers in the team's area.
    [Fact]
    public void Ending_a_teams_identifier_answers_with_the_team_codes()
    {
        var today = new DateOnly(2026, 11, 25);
        var identifier = ExternalIdentifier.ForTeam(Guid.CreateVersion7(), "KMSA_TEAM_NO", " KMSA-T-0147 ", today.AddDays(-5));

        (identifier.EntityType, identifier.Value).ShouldBe((ExternalIdentifierEntityType.Team, "KMSA-T-0147"));
        identifier.End(today.AddDays(1), today).Error.ShouldBe(RegistryErrors.TeamExternalIdValidityInvalid);
        identifier.End(today.AddDays(-6), today).Error.ShouldBe(RegistryErrors.TeamExternalIdValidityInvalid);
        identifier.End(today, today).IsSuccess.ShouldBeTrue();
        identifier.End(today, today).Error.ShouldBe(RegistryErrors.TeamExternalIdEnded);
        Should.Throw<ArgumentException>(() => ExternalIdentifier.ForTeam(Guid.Empty, "KMSA_TEAM_NO", "KMSA-T-0147"));
    }

    // S07 D18 (T3d): every domain value has the contract value of its name (the mappings are written by hand).
    [Fact]
    public void The_directory_maps_every_status_and_category_code_to_the_contract_value_of_its_name()
    {
        Enum.GetValues<TeamStatus>().Select(value => TeamDirectory.Map(value).ToString()).ShouldBe(Enum.GetNames<TeamStatus>());
        Enum.GetValues<GenderCategory>().Select(value => TeamDirectory.Map(value).ToString()).ShouldBe(Enum.GetNames<GenderCategory>());
        Enum.GetValues<AgeGroup>().Select(value => TeamDirectory.Map(value).ToString()).ShouldBe(Enum.GetNames<AgeGroup>());
        Enum.GetValues<TeamLevel>().Select(value => TeamDirectory.Map(value).ToString()).ShouldBe(Enum.GetNames<TeamLevel>());
        Enum.GetNames<Contracts.TeamStatus>().ShouldBe(Enum.GetNames<TeamStatus>());
        Enum.GetNames<Contracts.GenderCategory>().ShouldBe(Enum.GetNames<GenderCategory>());
        Enum.GetNames<Contracts.AgeGroup>().ShouldBe(Enum.GetNames<AgeGroup>());
        Enum.GetNames<Contracts.TeamLevel>().ShouldBe(Enum.GetNames<TeamLevel>());
    }

    private static Team NewTeam() =>
        Team.Create("TESTBALL_FULL", NtindaSwifts(), new DateOnly(2020, 1, 1), "ntinda-swifts-fc-senior-men", TeamSource.Manual, SystemActor.Instance, _now);
}
