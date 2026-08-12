using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using SportsPlatform.Modules.Notifications.Services;

namespace SportsPlatform.Api.Services;

public sealed class FirebasePushDeliveryClient : IPushDeliveryClient
{
    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<FirebasePushDeliveryClient> _logger;

    public FirebasePushDeliveryClient(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<FirebasePushDeliveryClient> logger)
    {
        _logger = logger;
        var configuredPath = configuration["Firebase:CredentialsPath"]
            ?? configuration["GOOGLE_APPLICATION_CREDENTIALS"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            _logger.LogInformation(
                "Firebase push delivery is disabled because no credentials path is configured.");
            return;
        }

        var credentialsPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(configuredPath, environment.ContentRootPath);
        if (!File.Exists(credentialsPath))
        {
            _logger.LogWarning(
                "Firebase push delivery is disabled because credentials were not found at {CredentialsPath}.",
                credentialsPath);
            return;
        }

        try
        {
            var options = new AppOptions
            {
                Credential = CredentialFactory
                    .FromFile<ServiceAccountCredential>(credentialsPath)
                    .ToGoogleCredential(),
                ProjectId = configuration["Firebase:ProjectId"]
            };
            var app = FirebaseApp.Create(options, $"sports-platform-{Guid.NewGuid():N}");
            _messaging = FirebaseMessaging.GetMessaging(app);
            _logger.LogInformation("Firebase push delivery is enabled.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Firebase push delivery is disabled because initialization failed.");
        }
    }

    public bool IsConfigured => _messaging is not null;

    public async Task SendToTopicsAsync(
        IEnumerable<string> topics,
        PushDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        if (_messaging is null) return;

        var data = new Dictionary<string, string>(delivery.Data, StringComparer.Ordinal)
        {
            ["notificationId"] = delivery.NotificationId,
            ["type"] = delivery.Type,
            ["payload"] = delivery.Payload
        };
        foreach (var topic in topics.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct())
        {
            try
            {
                await _messaging.SendAsync(new Message
                {
                    Topic = topic,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = delivery.Title,
                        Body = delivery.Body
                    },
                    Data = data,
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            ChannelId = "sports_platform_live_updates"
                        }
                    },
                    Apns = new ApnsConfig
                    {
                        Aps = new Aps { Sound = "default" }
                    }
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                // Push is an auxiliary channel and must never fail the scoring or scheduling write.
                _logger.LogError(exception,
                    "Firebase push delivery {NotificationId} failed for topic {Topic}.",
                    delivery.NotificationId, topic);
            }
        }
    }
}
