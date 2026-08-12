using FluentAssertions;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class ParticipantAbstractionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void EveryRequiredParticipantType_UsesTheSharedParticipantModel()
    {
        Participant[] participants =
        {
            new Team(Guid.NewGuid(), TenantId, "Team", "TM"),
            new Athlete(Guid.NewGuid(), TenantId, "Athlete", "AT"),
            new Pair(Guid.NewGuid(), TenantId, "Pair", "PR"),
            new RelayTeam(Guid.NewGuid(), TenantId, "Relay", "RL"),
            new Crew(Guid.NewGuid(), TenantId, "Crew", "CR"),
            new Club(Guid.NewGuid(), TenantId, "Club", "CL"),
            new School(Guid.NewGuid(), TenantId, "School", "SC"),
            new Country(Guid.NewGuid(), TenantId, "Country", "CO"),
            new CompositeTeam(Guid.NewGuid(), TenantId, "Composite", "CP")
        };

        participants.Select(item => item.Type).Should().BeEquivalentTo(
            Enum.GetValues<ParticipantType>());
        participants.Should().OnlyContain(item => item.TenantId == TenantId);
    }

    [Fact]
    public void ParticipantProfile_AttachesValidatedAttributesPerSport()
    {
        Participant athlete = new Athlete(Guid.NewGuid(), TenantId, "Runner", "RUN");
        var athletics = Guid.NewGuid();
        var rugby = Guid.NewGuid();

        athlete.SetSportAttributes(athletics, "{\"personalBest\":10.42}");
        athlete.SetSportAttributes(rugby, "{\"position\":\"Wing\"}");
        athlete.SetSportAttributes(athletics, "{\"personalBest\":10.31}");

        athlete.SportProfiles.Should().HaveCount(2);
        athlete.SportProfiles.Single(item => item.SportId == athletics)
            .AttributesJson.Should().Contain("10.31");
        var invalid = () => athlete.SetSportAttributes(Guid.NewGuid(), "[]");
        invalid.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Contest_AcceptsOneOrMoreConfiguredParticipantTypes()
    {
        var contest = new Contest(Guid.NewGuid(), TenantId, Guid.NewGuid(), "Ranked");
        contest.ConfigureParticipantTypes(new[] { "Athlete", "Pair" });

        contest.AddParticipant(Guid.NewGuid(), "athlete").ParticipantType
            .Should().Be("Athlete");
        contest.AddParticipant(Guid.NewGuid(), "Pair").ParticipantType
            .Should().Be("Pair");
        var rejected = () => contest.AddParticipant(Guid.NewGuid(), "Team");
        rejected.Should().Throw<InvalidOperationException>();
        var empty = () => contest.ConfigureParticipantTypes(Array.Empty<string>());
        empty.Should().Throw<ArgumentException>();
    }
}
