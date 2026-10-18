using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Platform.Infrastructure.Messaging;

/// <summary>Polls every module's outbox (docs/architecture/events-and-jobs.md); off when <c>Outbox:Enabled</c> is false.</summary>
internal sealed partial class OutboxHostedService(
    OutboxProcessor processor, IOptions<OutboxOptions> options, TimeProvider timeProvider, ILogger<OutboxHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // Drain: a full batch, or an aggregate's next message unblocked by this one, needn't wait a tick.
                while (await processor.ProcessOnceAsync(stoppingToken) > 0)
                {
                }
            }
#pragma warning disable CA1031 // A database outage must not stop the host; the next tick tries again.
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
            {
                LogPollFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox poll failed; retrying on the next tick")]
    private partial void LogPollFailed(Exception exception);
}
