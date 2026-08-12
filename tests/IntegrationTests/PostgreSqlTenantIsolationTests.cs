using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Participants.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class PostgreSqlTenantIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase($"sports_platform_test_{Guid.NewGuid():N}")
        .WithUsername("sports_platform_test")
        .WithPassword("sports_platform_test")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task ProtectedTenantMatrixAndRunningJobRecovery_AreEnforcedByPostgreSqlBackedApi()
    {
        await using var factory =
            new ApiWebApplicationFactory(_postgres.GetConnectionString());
        await TenantIsolationMatrix.AssertAsync(factory);

        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jobId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var actorId = await database.Users.Where(item => item.TenantId == tenantId)
                .Select(item => item.Id).FirstAsync();
            var job = new BulkImportJob(
                jobId, tenantId, "teams", $"postgres-recovery-{suffix}.csv",
                DuplicateHandling.Reject, actorId, null, 1,
                JsonSerializer.Serialize(new[]
                {
                    new TeamImportDto
                    {
                        RowNumber = 2,
                        Name = $"PostgreSQL Recovered Team {suffix}",
                        ShortName = $"P{suffix[..3]}"
                    }
                }));
            job.Start();
            database.BulkImportJobs.Add(job);
            await database.SaveChangesAsync();
        }

        var worker = new BulkImportWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new BulkImportQueue(), NullLogger<BulkImportWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                using var scope = factory.Services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
                if ((await database.BulkImportJobs.AsNoTracking()
                        .SingleAsync(item => item.Id == jobId)).Status
                    == BulkImportJobStatus.Completed)
                    break;
                await Task.Delay(200);
            }
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
            var job = await database.BulkImportJobs.AsNoTracking()
                .SingleAsync(item => item.Id == jobId);
            job.Status.Should().Be(BulkImportJobStatus.Completed);
            job.ImportedRows.Should().Be(1);
            job.InputJson.Should().Be("[]");
            (await database.Participants.CountAsync(item =>
                item.TenantId == tenantId
                && item.Name == $"PostgreSQL Recovered Team {suffix}"))
                .Should().Be(1);
            database.Participants.RemoveRange(database.Participants.Where(item =>
                item.Name == $"PostgreSQL Recovered Team {suffix}"));
            database.BulkImportJobs.RemoveRange(database.BulkImportJobs.Where(item => item.Id == jobId));
            await database.SaveChangesAsync();
        }
    }
}
