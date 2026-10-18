using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Development's <c>seed --reset</c>: drops every module schema (and any other schema a module maps to, like
/// Platform's <c>audit</c>) as their owner, then applies the migrations again. Dropping, not truncating, because the
/// append-only triggers reject TRUNCATE for the owner too. Only <see cref="Seeding.SeedRunner"/> calls it, after its
/// environment check.
/// </summary>
public sealed partial class DatabaseReset(
    IEnumerable<ModuleDatabase> databases,
    MigrationRunner migrationRunner,
    IConfiguration configuration,
    ILogger<DatabaseReset> logger)
{
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
            configuration, ConnectionStringNames.Migrator);

        var schemas = new List<string>();
        foreach (var database in databases)
        {
            await using var context = database.CreateContext(connectionString);
            schemas.AddRange(context.Model.GetEntityTypes().Select(entity => entity.GetSchema()).OfType<string>().Append(database.Schema));
        }

        await using (var context = databases.First().CreateContext(connectionString))
        {
            foreach (var schema in schemas.Distinct(StringComparer.Ordinal))
            {
                var sql = $"DROP SCHEMA IF EXISTS {TableAccessMigrationExtensions.Identifier(schema)} CASCADE"; // Validated identifier.
                await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                LogDropped(schema);
            }
        }

        await migrationRunner.RunAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reset: dropped schema {Schema}")]
    private partial void LogDropped(string schema);
}
