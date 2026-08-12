using FluentAssertions;
using SportsPlatform.Modules.Scheduling.Entities;
using SportsPlatform.Modules.Scheduling.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class SchedulingConstraintTests
{
    [Fact]
    public void Detector_CoversEveryRequiredSchedulingConstraint()
    {
        var tenantId = Guid.NewGuid();
        var policy = new CompetitionSchedulePolicy(Guid.NewGuid(), Guid.NewGuid(), tenantId);
        policy.Configure(90, 60, 20, 8 * 60, 20 * 60,
            new[] { new DateOnly(2026, 8, 3) });
        var participantId = Guid.NewGuid();
        var officialId = Guid.NewGuid();
        var events = new[]
        {
            Event("Opening match", "Main Field", new DateTime(2026, 8, 3, 7, 30, 0),
                participantId, officialId),
            Event("Second match", "main field", new DateTime(2026, 8, 3, 8, 0, 0),
                participantId, officialId),
            new CompetitionScheduleEvent(Guid.NewGuid(), "Final", "Main Field", null, 90,
                Array.Empty<Guid>(), Array.Empty<Guid>())
        };

        var conflicts = new CompetitionScheduleConflictDetector().Detect(events, policy);

        conflicts.Select(item => item.Type).Should().Contain(new[]
        {
            "VenueConflict",
            "ParticipantConflict",
            "MinimumRest",
            "OfficialConflict",
            "TimeWindow",
            "BlackoutDate",
            "Unscheduled"
        });
        conflicts.Select(item => item.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Override_RequiresAnAuditReason()
    {
        var action = () => new ScheduleConflictOverride(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "VenueConflict:a:b", " ", Guid.NewGuid());

        action.Should().Throw<ArgumentException>()
            .WithMessage("*override reason*");
    }

    private static CompetitionScheduleEvent Event(
        string name,
        string venue,
        DateTime start,
        Guid participantId,
        Guid officialId) => new(
            Guid.NewGuid(), name, venue, start, 90,
            new[] { participantId }, new[] { officialId });
}
