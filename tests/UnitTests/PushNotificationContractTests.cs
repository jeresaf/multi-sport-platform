using FluentAssertions;
using SportsPlatform.Modules.Notifications.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class PushNotificationContractTests
{
    [Fact]
    public void ContestTopics_MatchMobileSubscriptions_AndContainNoDuplicates()
    {
        var contestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var competitionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        PushTopics.Contest("Nairobi Rugby Club", contestId, competitionId)
            .Should().Equal(
                "tenant-nairobi-rugby-club",
                $"favourite-contest-{contestId}",
                $"favourite-competition-{competitionId}");
    }

    [Fact]
    public void AlertTopics_MatchFineGrainedMobilePreferences()
    {
        var contestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var competitionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        PushTopics.ContestAlert("Nairobi Rugby Club", contestId, competitionId,
                PushNotificationTypes.ScoreChanged)
            .Should().Equal(
                "tenant-nairobi-rugby-club",
                $"favourite-contest-{contestId}-score_changed",
                $"favourite-competition-{competitionId}-score_changed");
    }

    [Fact]
    public void NotificationTypes_CoverAuthoritativeTaskList()
    {
        new[]
        {
            PushNotificationTypes.FixtureReminder,
            PushNotificationTypes.ContestStarted,
            PushNotificationTypes.ScoreChanged,
            PushNotificationTypes.PeriodEnded,
            PushNotificationTypes.FinalResult,
            PushNotificationTypes.Postponement,
            PushNotificationTypes.Qualification,
            PushNotificationTypes.Announcement
        }.Should().OnlyHaveUniqueItems().And.NotContain(string.Empty);
    }

    [Theory]
    [InlineData("Tenant-Uganda Sports Demo", "tenant-uganda-sports-demo")]
    [InlineData(" favourite-contest-ABC ", "favourite-contest-abc")]
    public void TopicNormalization_MatchesMobileTopicRules(string input, string expected)
    {
        PushTopics.Normalize(input).Should().Be(expected);
    }
}
