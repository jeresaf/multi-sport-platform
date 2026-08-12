using System.Threading.Channels;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Participants.Services;

namespace SportsPlatform.Api.Services;

public interface IBulkImportQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class BulkImportQueue : IBulkImportQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(jobId, cancellationToken);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class BulkImportJobProcessor(
    SportsPlatformDbContext database, BulkImportService imports, IAuditService audit)
{
    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await database.BulkImportJobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null || job.Status is BulkImportJobStatus.Completed or BulkImportJobStatus.Failed) return;
        try
        {
            job.Start(); await database.SaveChangesAsync(cancellationToken);
            BulkImportResult result = job.Kind switch
            {
                "athletes" => await imports.ImportAthletesAsync(job.TenantId,
                    JsonSerializer.Deserialize<List<AthleteImportDto>>(job.InputJson) ?? [], job.DuplicateHandling, cancellationToken),
                "teams" => await imports.ImportTeamsAsync(job.TenantId,
                    JsonSerializer.Deserialize<List<TeamImportDto>>(job.InputJson) ?? [], job.DuplicateHandling, cancellationToken),
                "roster-entries" when job.RosterId.HasValue => await imports.ImportRosterEntriesAsync(job.TenantId,
                    Guid.Empty, job.RosterId.Value,
                    JsonSerializer.Deserialize<List<RosterEntryImportDto>>(job.InputJson) ?? [], job.DuplicateHandling, cancellationToken),
                _ => throw new InvalidOperationException($"Import kind '{job.Kind}' is not supported.")
            };
            job.Complete(result); await database.SaveChangesAsync(cancellationToken);
            await audit.RecordAsync("BulkImportCompleted", "BulkImportJob", job.Id.ToString(), job.TenantId,
                job.CreatedBy, null, JsonSerializer.Serialize(new { job.Kind, job.TotalRows, job.ImportedRows,
                    job.DuplicateRows, job.FailedRows }), "Bulk import processed", cancellationToken);
        }
        catch (Exception exception)
        {
            // A provider failure can leave the failed row tracked. Clear it so
            // recording the durable job failure cannot retry that row.
            database.ChangeTracker.Clear();
            job = await database.BulkImportJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
            job.Fail(exception.Message); await database.SaveChangesAsync(cancellationToken);
            await audit.RecordAsync("BulkImportFailed", "BulkImportJob", job.Id.ToString(), job.TenantId,
                job.CreatedBy, null, null, exception.Message, cancellationToken);
        }
    }
}

public sealed class BulkImportWorker(
    IServiceScopeFactory scopeFactory, IBulkImportQueue queue, ILogger<BulkImportWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                foreach (var id in await database.BulkImportJobs.Where(item =>
                        item.Status == BulkImportJobStatus.Queued
                        || item.Status == BulkImportJobStatus.Running)
                    .OrderBy(item => item.CreatedAt).Select(item => item.Id).ToArrayAsync(stoppingToken))
                    await queue.EnqueueAsync(id, stoppingToken);
            }
            await foreach (var id in queue.ReadAllAsync(stoppingToken))
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BulkImportJobProcessor>().ProcessAsync(id, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogError(exception, "Bulk import worker stopped unexpectedly."); }
    }
}
