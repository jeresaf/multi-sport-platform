using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Participants.Services;
using SportsPlatform.Modules.Reporting.Entities;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class BackgroundJobRecoveryTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly Guid PremierTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid NbaTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly ApiWebApplicationFactory _factory;

    public BackgroundJobRecoveryTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BulkImportWorker_RecoversRunningJobsAndContinuesAfterIsolatedFailure()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var recoveredJobId = Guid.NewGuid();
        var failedJobId = Guid.NewGuid();
        Guid actorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            actorId = await database.Users.Where(item => item.TenantId == PremierTenantId)
                .Select(item => item.Id).FirstAsync();
            var recovered = new BulkImportJob(
                recoveredJobId, PremierTenantId, "teams", $"recovery-{suffix}.csv",
                DuplicateHandling.Reject, actorId, null, 1,
                JsonSerializer.Serialize(new[]
                {
                    new TeamImportDto
                    {
                        RowNumber = 2,
                        Name = $"Recovered Team {suffix}",
                        ShortName = $"R{suffix[..3]}"
                    }
                }));
            recovered.Start();
            var failed = new BulkImportJob(
                failedJobId, NbaTenantId, "unsupported", $"failure-{suffix}.csv",
                DuplicateHandling.Reject, actorId, null, 1, "[]");
            database.BulkImportJobs.AddRange(failed, recovered);
            await database.SaveChangesAsync();
        }

        var queue = new BulkImportQueue();
        var worker = new BulkImportWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            queue,
            NullLogger<BulkImportWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                var statuses = await database.BulkImportJobs.AsNoTracking()
                    .Where(item => item.Id == recoveredJobId || item.Id == failedJobId)
                    .ToDictionaryAsync(item => item.Id, item => item.Status);
                return statuses.GetValueOrDefault(recoveredJobId) == BulkImportJobStatus.Completed
                    && statuses.GetValueOrDefault(failedJobId) == BulkImportJobStatus.Failed;
            }, TimeSpan.FromSeconds(15));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var recovered = await database.BulkImportJobs.AsNoTracking()
                .SingleAsync(item => item.Id == recoveredJobId);
            var failed = await database.BulkImportJobs.AsNoTracking()
                .SingleAsync(item => item.Id == failedJobId);
            recovered.ImportedRows.Should().Be(1);
            recovered.InputJson.Should().Be("[]");
            failed.InputJson.Should().Be("[]");
            failed.GetErrors().Should().Contain(item => item.Message.Contains("not supported"));
            (await database.Participants.CountAsync(item =>
                item.TenantId == PremierTenantId && item.Name == $"Recovered Team {suffix}"))
                .Should().Be(1);
            (await database.Participants.AnyAsync(item =>
                item.TenantId == NbaTenantId && item.Name == $"Recovered Team {suffix}"))
                .Should().BeFalse();

            database.Participants.RemoveRange(database.Participants.Where(item =>
                item.Name == $"Recovered Team {suffix}"));
            database.BulkImportJobs.RemoveRange(database.BulkImportJobs.Where(item =>
                item.Id == recoveredJobId || item.Id == failedJobId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ReportWorker_RecoversPendingAndProcessingReportsAcrossTenants()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var pendingId = Guid.NewGuid();
        var processingId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var actorId = await database.Users.Select(item => item.Id).FirstAsync();
            var pending = CreateReport(pendingId, PremierTenantId, actorId, $"Pending {suffix}");
            var processing = CreateReport(processingId, NbaTenantId, actorId, $"Processing {suffix}");
            processing.MarkAsProcessing();
            database.Reports.AddRange(pending, processing);
            await database.SaveChangesAsync();
        }

        var queue = new ReportGenerationQueue();
        var worker = new ReportGenerationWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            queue,
            NullLogger<ReportGenerationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                return await database.Reports.AsNoTracking().CountAsync(item =>
                    (item.Id == pendingId || item.Id == processingId)
                    && item.Status == ReportStatus.Completed) == 2;
            }, TimeSpan.FromSeconds(30));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var reports = await database.Reports.AsNoTracking()
                .Where(item => item.Id == pendingId || item.Id == processingId)
                .ToArrayAsync();
            reports.Should().OnlyContain(item => item.GeneratedContent!.Length > 0
                && item.GeneratedContentType!.StartsWith("text/csv", StringComparison.Ordinal));
            reports.Single(item => item.Id == pendingId).TenantId.Should().Be(PremierTenantId);
            reports.Single(item => item.Id == processingId).TenantId.Should().Be(NbaTenantId);
            database.Reports.RemoveRange(database.Reports.Where(item =>
                item.Id == pendingId || item.Id == processingId));
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task NotificationWorker_HonorsTenantPauseThenResumesOnlyThatBacklog()
    {
        var premierNotificationId = Guid.NewGuid();
        var nbaNotificationId = Guid.NewGuid();
        Guid premierUserId;
        Guid nbaUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            premierUserId = await database.Users.Where(item => item.TenantId == PremierTenantId)
                .Select(item => item.Id).FirstAsync();
            nbaUserId = await database.Users.Where(item => item.TenantId == NbaTenantId)
                .Select(item => item.Id).FirstAsync();
            database.Notifications.AddRange(
                new Notification(premierNotificationId, premierUserId, NotificationType.System,
                    NotificationChannel.InApp, "Premier queued", "Must remain paused."),
                new Notification(nbaNotificationId, nbaUserId, NotificationType.System,
                    NotificationChannel.InApp, "NBA queued", "Must be delivered."));
            database.AuditEntries.Add(new AuditEntry(
                Guid.NewGuid(), PremierTenantId, null,
                "PlatformSupportDisabledNotifications", "TenantNotificationDelivery",
                PremierTenantId.ToString(), null,
                JsonSerializer.Serialize(new { disabled = true, disabledUntilUtc = DateTime.UtcNow.AddMinutes(5) }),
                "Background worker pause test"));
            await database.SaveChangesAsync();
        }

        var worker = new NotificationDeliveryWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationDeliveryWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                return (await database.Notifications.AsNoTracking()
                    .SingleAsync(item => item.Id == nbaNotificationId)).Status
                    == NotificationStatus.Sent;
            }, TimeSpan.FromSeconds(10));

            using (var scope = _factory.Services.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                (await database.Notifications.AsNoTracking()
                    .SingleAsync(item => item.Id == premierNotificationId)).Status
                    .Should().Be(NotificationStatus.Pending);
                database.AuditEntries.Add(new AuditEntry(
                    Guid.NewGuid(), PremierTenantId, null,
                    "PlatformSupportEnabledNotifications", "TenantNotificationDelivery",
                    PremierTenantId.ToString(), null,
                    JsonSerializer.Serialize(new { disabled = false, disabledUntilUtc = (DateTime?)null }),
                    "Background worker resume test"));
                await database.SaveChangesAsync();
            }

            await WaitForAsync(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                return (await database.Notifications.AsNoTracking()
                    .SingleAsync(item => item.Id == premierNotificationId)).Status
                    == NotificationStatus.Sent;
            }, TimeSpan.FromSeconds(10));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            database.Notifications.RemoveRange(database.Notifications.Where(item =>
                item.Id == premierNotificationId || item.Id == nbaNotificationId));
            await database.SaveChangesAsync();
        }
    }

    private static Report CreateReport(Guid id, Guid tenantId, Guid actorId, string name)
    {
        var report = new Report(id, tenantId, actorId, name, ReportType.FixtureSchedule);
        report.SetFormat(ReportFormat.Csv);
        report.SetParameters(JsonSerializer.Serialize(new ReportParameters(null, null, null)));
        return report;
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Background condition was not met within {timeout}.");
    }
}
