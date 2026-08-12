using FluentAssertions;
using SportsPlatform.Modules.Participants.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class TeamProfileTests
{
    [Fact]
    public void TeamAndClubProfiles_ValidateIdentityLogoAndContact()
    {
        var tenantId = Guid.NewGuid();
        OrganizationParticipant team = new Team(Guid.NewGuid(), tenantId, "Lions", "LIO");
        OrganizationParticipant club = new Club(Guid.NewGuid(), tenantId, "Lions Club", "LC");

        team.UpdateProfile("City Lions", "CL", "https://assets.example/lions.png",
            "City Arena", "Coach One", "Manager One", "team@example.com", "+254700000001");
        club.UpdateProfile("City Lions Club", "CLC", null,
            "Club House", "Club Coach", "Club Manager", "club@example.com", null);

        team.HomeVenue.Should().Be("City Arena");
        club.ContactEmail.Should().Be("club@example.com");
        ((Action)(() => team.UpdateProfile("City Lions", "CL", "file:///logo.png",
            null, null, null, null, null))).Should().Throw<ArgumentException>();
        ((Action)(() => club.UpdateProfile("City Lions Club", "CLC", null,
            null, null, null, "invalid", null))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SeasonalRosters_ValidateDatesAndRejectDuplicates()
    {
        var team = Team();
        var starts = DateTime.UtcNow.Date;
        team.AddRoster("First team", "2026/27", starts, starts.AddMonths(9), null);

        team.Rosters.Single().Season.Should().Be("2026/27");
        ((Action)(() => team.AddRoster(
            "First team", "2026/27", starts, starts.AddMonths(9), null)))
            .Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
        ((Action)(() => team.AddRoster(
            "Reserve", "2027/28", starts, starts.AddDays(-1), null)))
            .Should().Throw<ArgumentException>().WithMessage("*end date*");
    }

    [Fact]
    public void RosterMovement_DeactivatesButRetainsHistoricalMembership()
    {
        var team = Team();
        var athleteId = Guid.NewGuid();
        var oldRoster = team.AddRoster("Under 21", "2025/26", null, null, null);
        var newRoster = team.AddRoster("Senior", "2026/27", null, null, null);
        oldRoster.AddAthlete(athleteId, "12", "Forward");

        oldRoster.RemoveAthlete(athleteId, "Promoted to senior roster");
        newRoster.AddAthlete(athleteId, "7", "Forward");

        var oldEntry = oldRoster.Entries.Single();
        oldEntry.IsActive.Should().BeFalse();
        oldEntry.LeftAt.Should().NotBeNull();
        oldEntry.MovementReason.Should().Be("Promoted to senior roster");
        newRoster.Entries.Single().IsActive.Should().BeTrue();
    }

    private static Team Team() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Lions", "LIO");
}
