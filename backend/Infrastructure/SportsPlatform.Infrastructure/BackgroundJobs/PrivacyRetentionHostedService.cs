using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;

namespace SportsPlatform.Infrastructure.BackgroundJobs;

public sealed class PrivacyRetentionHostedService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PrivacyRetentionHostedService> _logger;

    public PrivacyRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<PrivacyRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunDuePoliciesAsync(stoppingToken);
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunDuePoliciesAsync(stoppingToken);
    }

    private async Task RunDuePoliciesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var retention = scope.ServiceProvider.GetRequiredService<PrivacyRetentionService>();
            var due = await database.TenantRetentionPolicies
                .Where(item => item.IsActive && item.NextRunAt <= DateTime.UtcNow)
                .OrderBy(item => item.NextRunAt)
                .ToArrayAsync(cancellationToken);
            foreach (var policy in due)
                await retention.ExecuteAsync(policy, reason: "Scheduled tenant retention run", cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The scheduled privacy retention sweep failed.");
        }
    }
}
