using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SportsPlatform.IntegrationTests;

public sealed class DatabaseMigrationBaselineTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("sports_platform_migration_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Baseline_IsDiscoveredAndCreatesTheCompletePostgreSqlSchema()
    {
        var options = new DbContextOptionsBuilder<SportsPlatformDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new SportsPlatformDbContext(options);

        var migrations = context.Database.GetMigrations().ToArray();
        migrations.Should().ContainSingle(item =>
            item.EndsWith("_PlatformBaseline20260812", StringComparison.Ordinal));

        await context.Database.MigrateAsync();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        applied.Should().Equal(migrations);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE';
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().BeGreaterThanOrEqualTo(74);
        await connection.CloseAsync();

        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }
}
