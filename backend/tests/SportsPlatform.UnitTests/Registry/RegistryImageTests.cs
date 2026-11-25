using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.UnitTests.Registry;

// S07 D10, D15: a logo or badge is set while the record takes new children, removed in any status; each returns the asset
// it replaced or cleared, and a team's two images are independent.
public sealed class RegistryImageTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private static Organisation NtindaSwiftsClub() => Organisation.Create(
        new OrganisationDetails(OrganisationType.Club, "Ntinda Swifts SC", null, null, "UG", "Kampala", null, null, null, null, null, null),
        "ntinda-swifts-sc", OrganisationSource.Manual, SystemActor.Instance, _now);

    private static Team NtindaSwiftsFirstTeam() => Team.Create(
        "TESTBALL_FULL",
        new TeamDetails("Ntinda Swifts FC", null, new(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First), null, null, null, null, null, null, null, null),
        new DateOnly(2020, 1, 1), "ntinda-swifts-fc-senior-men", TeamSource.Manual, SystemActor.Instance, _now);

    [Theory]
    [InlineData(OrganisationStatus.Suspended, null)]
    [InlineData(OrganisationStatus.Dissolved, "ORGANISATION_DISSOLVED")]
    [InlineData(OrganisationStatus.Archived, "ORGANISATION_ARCHIVED")]
    public void An_organisation_logo_is_replaced_while_it_takes_children_and_removed_in_any_status(OrganisationStatus status, string? refusal)
    {
        var organisation = NtindaSwiftsClub();
        var first = Guid.CreateVersion7();
        organisation.SetLogo(first).Value.ShouldBeNull();
        organisation.ChangeStatus(status, "Merged into another club", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        var second = Guid.CreateVersion7();
        var set = organisation.SetLogo(second);

        set.Error?.Code.ShouldBe(refusal);
        var current = refusal is null ? second : first;
        if (refusal is null)
        {
            set.Value.ShouldBe(first);
        }

        organisation.LogoAssetId.ShouldBe(current);
        organisation.RemoveLogo().Value.ShouldBe(current);
        organisation.LogoAssetId.ShouldBeNull();
        organisation.RemoveLogo().Error.ShouldBe(RegistryErrors.OrganisationLogoNotFound);
    }

    [Fact]
    public void A_teams_logo_and_badge_are_set_and_removed_each_on_its_own()
    {
        var team = NtindaSwiftsFirstTeam();
        var logo = Guid.CreateVersion7();
        var badge = Guid.CreateVersion7();

        team.SetLogo(logo).Value.ShouldBeNull();
        team.SetBadge(badge).Value.ShouldBeNull();
        var newBadge = Guid.CreateVersion7();
        team.SetBadge(newBadge).Value.ShouldBe(badge);

        (team.LogoAssetId, team.BadgeAssetId).ShouldBe((logo, newBadge));
        team.RemoveLogo().Value.ShouldBe(logo);
        (team.LogoAssetId, team.BadgeAssetId).ShouldBe((null, newBadge));
        team.RemoveLogo().Error.ShouldBe(RegistryErrors.TeamLogoNotFound);
        team.RemoveBadge().Value.ShouldBe(newBadge);
        team.RemoveBadge().Error.ShouldBe(RegistryErrors.TeamBadgeNotFound);
    }

    [Theory]
    [InlineData(TeamStatus.Disbanded, "TEAM_DISBANDED")]
    [InlineData(TeamStatus.Archived, "TEAM_ARCHIVED")]
    public void A_disbanded_or_archived_team_takes_no_new_image_but_loses_one(TeamStatus status, string refusal)
    {
        var team = NtindaSwiftsFirstTeam();
        var logo = Guid.CreateVersion7();
        team.SetLogo(logo).IsSuccess.ShouldBeTrue();
        team.ChangeStatus(status, "Folded at the end of the season", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        team.SetLogo(Guid.CreateVersion7()).Error!.Code.ShouldBe(refusal);
        team.SetBadge(Guid.CreateVersion7()).Error!.Code.ShouldBe(refusal);

        (team.LogoAssetId, team.BadgeAssetId).ShouldBe((logo, null));
        team.RemoveLogo().Value.ShouldBe(logo);
    }
}
