using FluentAssertions;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Results.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class ContestStateProjectionTests
{
    [Theory]
    [InlineData(ContestStatus.Draft, CanonicalContestStatus.NotStarted, "NOT_STARTED")]
    [InlineData(ContestStatus.Scheduled, CanonicalContestStatus.NotStarted, "NOT_STARTED")]
    [InlineData(ContestStatus.Ready, CanonicalContestStatus.StartingSoon, "STARTING_SOON")]
    [InlineData(ContestStatus.InProgress, CanonicalContestStatus.Live, "LIVE")]
    [InlineData(ContestStatus.Paused, CanonicalContestStatus.Interrupted, "INTERRUPTED")]
    [InlineData(ContestStatus.Suspended, CanonicalContestStatus.Suspended, "SUSPENDED")]
    [InlineData(ContestStatus.Completed, CanonicalContestStatus.Ended, "ENDED")]
    [InlineData(ContestStatus.Cancelled, CanonicalContestStatus.Cancelled, "CANCELLED")]
    [InlineData(ContestStatus.Abandoned, CanonicalContestStatus.Abandoned, "ABANDONED")]
    public void ContestLifecycleProjection_MapsOperationalStates(
        ContestStatus legacy,
        CanonicalContestStatus canonical,
        string apiCode)
    {
        legacy.ToCanonical().Should().Be(canonical);
        legacy.ToCanonical().ToApiCode().Should().Be(apiCode);
    }

    [Theory]
    [InlineData(ContestStatus.PendingApproval)]
    [InlineData(ContestStatus.Approved)]
    [InlineData(ContestStatus.Corrected)]
    [InlineData(ContestStatus.Published)]
    public void ContestLifecycleProjection_CollapsesLegacyResultStatesToEnded(
        ContestStatus legacy)
    {
        legacy.IsLegacyResultWorkflowState().Should().BeTrue();
        legacy.ToCanonical().Should().Be(CanonicalContestStatus.Ended);
    }

    [Fact]
    public void ResultWorkflowProjection_IsIndependentFromContestLifecycle()
    {
        ContestResultWorkflowProjection.Summarize(Array.Empty<ContestResultStatus>())
            .Should().Be("NONE");
        ContestResultWorkflowProjection.Summarize(new[] { ContestResultStatus.Submitted })
            .Should().Be("SUBMITTED");
        ContestResultWorkflowProjection.Summarize(new[] { ContestResultStatus.UnderReview })
            .Should().Be("UNDER_REVIEW");
        ContestResultWorkflowProjection.Summarize(new[] { ContestResultStatus.Approved })
            .Should().Be("APPROVED");
        ContestResultWorkflowProjection.Summarize(new[]
            {
                ContestResultStatus.Published,
                ContestResultStatus.Official
            })
            .Should().Be("PUBLISHED");
        ContestResultWorkflowProjection.Summarize(new[] { ContestResultStatus.Corrected })
            .Should().Be("CORRECTED");
    }
}
