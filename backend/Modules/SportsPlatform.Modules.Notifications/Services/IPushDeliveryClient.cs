using System.Text.RegularExpressions;

namespace SportsPlatform.Modules.Notifications.Services;

public sealed record PushDelivery(
    string NotificationId,
    string Type,
    string Title,
    string Body,
    string Payload,
    IReadOnlyDictionary<string, string> Data);

public interface IPushDeliveryClient
{
    bool IsConfigured { get; }

    Task SendToTopicsAsync(
        IEnumerable<string> topics,
        PushDelivery delivery,
        CancellationToken cancellationToken = default);
}

public static partial class PushTopics
{
    public static string Tenant(string tenantSlug) => Build("tenant", tenantSlug);

    public static string FavouriteContest(Guid contestId) => Build("favourite-contest", contestId.ToString());

    public static string FavouriteCompetition(Guid competitionId) =>
        Build("favourite-competition", competitionId.ToString());

    public static IReadOnlyList<string> Contest(
        string tenantSlug,
        Guid contestId,
        Guid? competitionId)
    {
        var topics = new List<string>
        {
            Tenant(tenantSlug),
            FavouriteContest(contestId)
        };
        if (competitionId.HasValue)
            topics.Add(FavouriteCompetition(competitionId.Value));
        return topics;
    }

    private static string Build(string prefix, string value)
    {
        var topic = $"{prefix}-{value}".ToLowerInvariant();
        topic = InvalidTopicCharacter().Replace(topic, "-").Trim('-');
        if (topic.Length > 900) topic = topic[..900].TrimEnd('-');
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("A push topic cannot be empty.", nameof(value));
        return topic;
    }

    [GeneratedRegex("[^a-z0-9-_.~%]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidTopicCharacter();
}

public static class PushNotificationTypes
{
    public const string FixtureReminder = "fixture_reminder";
    public const string ContestStarted = "contest_started";
    public const string ScoreChanged = "score_changed";
    public const string PeriodEnded = "period_ended";
    public const string FinalResult = "final_result";
    public const string Postponement = "postponement";
    public const string Qualification = "qualification";
    public const string Announcement = "announcement";
}
