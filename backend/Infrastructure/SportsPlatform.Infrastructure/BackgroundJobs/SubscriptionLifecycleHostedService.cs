using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Infrastructure.BackgroundJobs;

public sealed class SubscriptionLifecycleHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionLifecycleHostedService> _logger;

    public SubscriptionLifecycleHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionLifecycleHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAllTenantsAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessAllTenantsAsync(stoppingToken);
    }

    private async Task ProcessAllTenantsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var database = scope.ServiceProvider
                .GetRequiredService<SportsPlatformDbContext>();
            var tenantIds = await database.Subscriptions
                .AsNoTracking()
                .Where(item => item.Status != SubscriptionStatus.Archived)
                .Select(item => item.TenantId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
            var job = scope.ServiceProvider
                .GetRequiredService<SubscriptionLifecycleJob>();
            foreach (var tenantId in tenantIds)
                await job.ExecuteAsync(tenantId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "The scheduled subscription lifecycle sweep failed.");
        }
    }
}
