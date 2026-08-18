using FluentAssertions;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Results.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class CanonicalStatusContractTests
{
    [Theory]
    [InlineData(ContestStatus.Scheduled, CanonicalContestStatus.NotStarted, "NOT_STARTED")]
    [InlineData(ContestStatus.Ready, CanonicalContestStatus.StartingSoon, "STARTING_SOON")]
    [InlineData(ContestStatus.InProgress, CanonicalContestStatus.Live, "LIVE")]
    [InlineData(ContestStatus.Paused, CanonicalContestStatus.Interrupted, "INTERRUPTED")]
    [InlineData(ContestStatus.Completed, CanonicalContestStatus.Ended, "ENDED")]
    [InlineData(ContestStatus.Published, CanonicalContestStatus.Closed, "CLOSED")]
    [InlineData(ContestStatus.Abandoned, CanonicalContestStatus.Abandoned, "ABANDONED")]
    public void LegacyContestStatuses_MapToStableCanonicalCodes(
        ContestStatus legacy,
        CanonicalContestStatus expected,
        string code)
    {
        legacy.ToCanonical().Should().Be(expected);
        expected.ToCode().Should().Be(code);
    }

    [Theory]
    [InlineData("in progress", CanonicalContestStatus.Live)]
    [InlineData("CANCELED", CanonicalContestStatus.Cancelled)]
    [InlineData("postponed", CanonicalContestStatus.Postponed)]
    public void KnownProviderStatuses_ParseWithoutLeakingProviderCodes(
        string raw,
        CanonicalContestStatus expected)
    {
        CanonicalContestStatusExtensions.TryParseProviderStatus(raw, out var status)
            .Should().BeTrue();
        status.Should().Be(expected);
    }

    [Fact]
    public void UnknownProviderStatus_FailsSafely()
    {
        CanonicalContestStatusExtensions.TryParseProviderStatus(
            "provider_magic_state",
            out var status).Should().BeFalse();
        status.Should().Be(CanonicalContestStatus.NotStarted);
    }

    [Theory]
    [InlineData(ContestResultStatus.Pending, ResultStatus.None, "NONE")]
    [InlineData(ContestResultStatus.Submitted, ResultStatus.Submitted, "SUBMITTED")]
    [InlineData(ContestResultStatus.UnderReview, ResultStatus.UnderReview, "UNDER_REVIEW")]
    [InlineData(ContestResultStatus.Official, ResultStatus.Approved, "APPROVED")]
    [InlineData(ContestResultStatus.Corrected, ResultStatus.Corrected, "CORRECTED")]
    public void LegacyResultStatuses_MapSeparatelyFromContestLifecycle(
        ContestResultStatus legacy,
        ResultStatus expected,
        string code)
    {
        legacy.ToCanonical().Should().Be(expected);
        expected.ToCode().Should().Be(code);
    }

    [Fact]
    public void ContestTransition_UpdatesDurableCanonicalStateAndHistoryContext()
    {
        var actorId = Guid.NewGuid();
        var contest = new Contest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "HeadToHead");

        contest.TransitionTo(
            ContestStatus.Cancelled,
            actorId: actorId,
            reason: "Venue unavailable");

        contest.CanonicalStatus.Should().Be(CanonicalContestStatus.Cancelled);
        contest.StatusReason.Should().Be("Venue unavailable");
        contest.CanonicalStatusChangedAt.Should().BeOnOrAfter(contest.CreatedAt);
        contest.PendingTransitions.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                CanonicalFrom = CanonicalContestStatus.NotStarted,
                CanonicalTo = CanonicalContestStatus.Cancelled,
                ActorId = (Guid?)actorId,
                Reason = "Venue unavailable"
            },
            options => options.ExcludingMissingMembers());
    }
}
