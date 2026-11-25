using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>
/// A team's names (S07-T3c; D8, D10, D18): the rename and its former name with the short name, code and dates (owner,
/// T3c plan), the date rules, the name on a date, nicknames and spellings, and the statuses that take none.
/// </summary>
public sealed class TeamNameTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _today = new(2026, 11, 24);
    private static readonly DateOnly _since = new(2020, 1, 1);
    private static readonly TeamCategory _seniorMen = new(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First);

    [Fact]
    public void A_rename_keeps_the_old_name_with_its_short_name_code_and_dates_and_keeps_the_slug()
    {
        var team = NewTeam();

        team.Rename("Bunga Breakers FC", "Bunga Breakers", "BBK", new DateOnly(2025, 7, 1), _today).IsSuccess.ShouldBeTrue();

        (team.Name, team.ShortName, team.Code, team.NameSince, team.Slug)
            .ShouldBe(("Bunga Breakers FC", "Bunga Breakers", "BBK", new DateOnly(2025, 7, 1), "bunga-lakeside-fc-senior-men"));
        (team.NameKey, team.ShortNameKey, team.SortKey).ShouldBe(("breakers bunga fc", "breakers bunga", "bunga breakers fc"));
        var former = team.Aliases.ShouldHaveSingleItem();
        (former.TeamId, former.Kind, former.Name, former.NameKey, former.ShortName, former.Code, former.ValidFrom, former.ValidTo)
            .ShouldBe((team.Id, AliasKind.FormerName, "Bunga Lakeside FC", "bunga fc lakeside", "Bunga Lakeside", "BLS", _since, new DateOnly(2025, 6, 30)));
    }

    [Fact]
    public void The_name_on_a_date_is_the_one_whose_dates_cover_it_and_the_first_name_before_it_began()
    {
        var team = NewTeam();
        team.Rename("Bunga Breakers FC", "Bunga Breakers", "BBK", new DateOnly(2023, 1, 1), _today).IsSuccess.ShouldBeTrue();
        team.Rename("Bunga Lakers FC", null, null, new DateOnly(2025, 7, 1), _today).IsSuccess.ShouldBeTrue();

        team.NameOn(new DateOnly(2019, 12, 31)).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", "BLS"));
        team.NameOn(_since).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", "BLS"));
        team.NameOn(new DateOnly(2022, 12, 31)).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", "BLS"));
        team.NameOn(new DateOnly(2023, 1, 1)).ShouldBe(new TeamNameOnDate("Bunga Breakers FC", "Bunga Breakers", "BBK"));
        team.NameOn(new DateOnly(2025, 6, 30)).ShouldBe(new TeamNameOnDate("Bunga Breakers FC", "Bunga Breakers", "BBK"));
        team.NameOn(new DateOnly(2025, 7, 1)).ShouldBe(new TeamNameOnDate("Bunga Lakers FC", null, null));
        team.NameOn(_today.AddYears(1)).ShouldBe(new TeamNameOnDate("Bunga Lakers FC", null, null));
    }

    [Fact]
    public void A_team_never_renamed_answers_its_name_on_every_date()
    {
        var team = NewTeam();

        team.NameOn(new DateOnly(1990, 1, 1)).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", "BLS"));
        team.NameOn(_today).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", "BLS"));
    }

    [Fact]
    public void A_rename_takes_effect_after_the_current_names_start_and_not_after_today()
    {
        var team = NewTeam();

        foreach (var effectiveOn in new[] { _since, _since.AddDays(-1), _today.AddDays(1) })
        {
            team.Rename("Bunga Breakers FC", null, null, effectiveOn, _today).Error!.Code.ShouldBe("TEAM_RENAME_DATE_INVALID");
        }

        team.Rename("Bunga Breakers FC", null, null, _today, _today).IsSuccess.ShouldBeTrue();
        team.Rename("Bunga Lakers FC", null, null, _today, _today).Error!.Code.ShouldBe("TEAM_RENAME_DATE_INVALID");
        team.Rename("Bunga Lakeside FC", null, null, _since.AddDays(1), _today).Error!.Code.ShouldBe("TEAM_RENAME_DATE_INVALID");
        team.Aliases.ShouldHaveSingleItem();
    }

    [Fact]
    public void The_same_name_exactly_is_no_rename_but_a_new_spelling_or_an_earlier_name_is()
    {
        var team = NewTeam();

        team.Rename("Bunga Lakeside FC", "Lakeside", null, new DateOnly(2024, 1, 1), _today).Error!.Code.ShouldBe("TEAM_NAME_UNCHANGED");
        team.Rename("Bunga Lakeside F.C.", "Bunga Lakeside", "BLS", new DateOnly(2024, 1, 1), _today).IsSuccess.ShouldBeTrue();
        team.Rename("Bunga Lakeside FC", "Bunga Lakeside", "BLS", new DateOnly(2025, 1, 1), _today).IsSuccess.ShouldBeTrue();

        team.Aliases.Select(alias => (alias.Name, alias.ValidFrom, alias.ValidTo)).ShouldBe(
        [
            ("Bunga Lakeside FC", _since, new DateOnly(2023, 12, 31)),
            ("Bunga Lakeside F.C.", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)),
        ]);
    }

    [Theory]
    [InlineData(TeamStatus.Inactive, null)]
    [InlineData(TeamStatus.Suspended, null)]
    [InlineData(TeamStatus.Disbanded, "TEAM_DISBANDED")]
    [InlineData(TeamStatus.Archived, "TEAM_ARCHIVED")]
    public void A_disbanded_or_archived_team_takes_no_new_name_or_alias(TeamStatus status, string? refusal)
    {
        var team = NewTeam();
        team.AddAlias("The Lakers", AliasKind.Nickname).IsSuccess.ShouldBeTrue();
        var nickname = team.Aliases.Single();
        if (status == TeamStatus.Archived)
        {
            team.ChangeStatus(TeamStatus.Disbanded, "Not enough players", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        }

        team.ChangeStatus(status, "Set up by the test", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        var renamed = team.Rename("Bunga Breakers FC", null, null, new DateOnly(2025, 7, 1), _today);
        var added = team.AddAlias("Bunga Lake Side", AliasKind.Spelling);

        (renamed.IsSuccess ? null : renamed.Error!.Code).ShouldBe(refusal);
        (added.IsSuccess ? null : added.Error!.Code).ShouldBe(refusal);
        team.RemoveAlias(nickname.Id, "Never used", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void An_alias_the_team_is_already_known_by_is_refused_former_names_included()
    {
        var team = NewTeam();
        team.Rename("Bunga Breakers FC", null, null, new DateOnly(2025, 7, 1), _today).IsSuccess.ShouldBeTrue();
        team.AddAlias("The Breakers", AliasKind.Nickname).IsSuccess.ShouldBeTrue();

        foreach (var name in new[] { "FC, Breakers Bunga", "the BREAKERS", "Bunga Lakeside F.C." })
        {
            team.AddAlias(name, AliasKind.Spelling).Error!.Code.ShouldBe("TEAM_ALIAS_EXISTS");
        }

        var nickname = team.Aliases.Single(alias => alias.Kind == AliasKind.Nickname);
        team.RemoveAlias(nickname.Id, "Never used", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        team.AddAlias("The Breakers", AliasKind.Spelling).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Only_a_current_nickname_or_spelling_of_the_team_can_be_removed()
    {
        var team = NewTeam();
        team.Rename("Bunga Breakers FC", null, null, new DateOnly(2025, 7, 1), _today).IsSuccess.ShouldBeTrue();
        team.AddAlias("The Breakers", AliasKind.Nickname).IsSuccess.ShouldBeTrue();
        var former = team.Aliases.Single(alias => alias.IsFormerName);
        var nickname = team.Aliases.Single(alias => !alias.IsFormerName);

        team.RemoveAlias(Guid.CreateVersion7(), "Misspelt", SystemActor.Instance, _now).Error!.Code.ShouldBe("TEAM_ALIAS_NOT_FOUND");
        team.RemoveAlias(former.Id, "Misspelt", SystemActor.Instance, _now).Error!.Code.ShouldBe("TEAM_ALIAS_FORMER_NAME");
        team.RemoveAlias(nickname.Id, " Never used ", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();
        (nickname.RemoveReason, nickname.RemovedAt, nickname.RemovedBy).ShouldBe(("Never used", _now, SystemActor.Instance.ActorId));
        team.RemoveAlias(nickname.Id, "Misspelt", SystemActor.Instance, _now).Error!.Code.ShouldBe("TEAM_ALIAS_REMOVED");
        former.IsRemoved.ShouldBeFalse();
    }

    [Fact]
    public void A_former_name_comes_only_from_a_rename()
    {
        Should.Throw<ArgumentException>(() => TeamAlias.Other(Guid.CreateVersion7(), "Bunga Lakeside FC", AliasKind.FormerName));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TeamAlias.FormerName(Guid.CreateVersion7(), "Bunga Lakeside FC", null, null, new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 1)));
        Should.Throw<ArgumentException>(() => TeamAlias.Other(Guid.CreateVersion7(), "--", AliasKind.Spelling));
    }

    [Fact]
    public void A_conflict_on_an_alias_is_the_teams()
    {
        RegistryConcurrencyConflicts.For(typeof(TeamAlias)).ShouldBeNull();
    }

    private static Team NewTeam() => Team.Create(
        "TESTBALL_FULL",
        new TeamDetails("Bunga Lakeside FC", "Bunga Lakeside", _seniorMen, null, "BLS", null, null, null, null, null, null),
        _since,
        "bunga-lakeside-fc-senior-men",
        TeamSource.Manual,
        SystemActor.Instance,
        _now);
}
