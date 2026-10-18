using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Development's <c>seed --reset</c>: drops every module schema (and any other schema a module maps to, like
/// Platform's <c>audit</c>) as their owner, then applies the migrations again. Dropping, not truncating, because the
/// append-only triggers reject TRUNCATE for the owner too. Refuses to run outside Development, whoever calls it; the
/// drops are one transaction.
/// </summary>
public sealed partial class DatabaseReset(
    IEnumerable<ModuleDatabase> databases,
    MigrationRunner migrationRunner,
    IConfiguration configuration,
    SeedEnvironment environment,
    ILogger<DatabaseReset> logger)
{
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(environment.Name, SeedEnvironmentPolicy.Development, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"A database reset is allowed only in Development (environment: {environment.Name}).");
        }

        var connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
            configuration, ConnectionStringNames.Migrator);

        var schemas = new List<string>();
        foreach (var database in databases)
        {
            await using var context = database.CreateContext(connectionString);
            schemas.AddRange(context.Model.GetEntityTypes().Select(entity => entity.GetSchema()).OfType<string>().Append(database.Schema));
        }

        var distinct = schemas.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.FirstOrDefault(IsSystemSchema) is { } system)
        {
            throw new InvalidOperationException($"A module maps to schema '{system}', which a reset must never drop.");
        }

        await using (var context = databases.First().CreateContext(connectionString))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            foreach (var schema in distinct)
            {
                var sql = $"DROP SCHEMA IF EXISTS {TableAccessMigrationExtensions.Identifier(schema)} CASCADE"; // Validated identifier.
                await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var schema in distinct)
        {
            LogDropped(schema);
        }

        await migrationRunner.RunAsync(cancellationToken);
    }

    private static bool IsSystemSchema(string schema) =>
        schema is "public" or "information_schema" || schema.StartsWith("pg_", StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reset: dropped schema {Schema}")]
    private partial void LogDropped(string schema);
}
