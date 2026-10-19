using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Applies every module's migrations as <c>sp_migrator</c>, in module registration order, then the post-migration
/// steps (<see cref="IPostMigrationStep"/>).
/// </summary>
public sealed partial class MigrationRunner(
    IEnumerable<ModuleDatabase> databases,
    IEnumerable<IPostMigrationStep> steps,
    IConfiguration configuration,
    ILogger<MigrationRunner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
            configuration, ConnectionStringNames.Migrator);

        foreach (var database in databases)
        {
            await using var context = database.CreateContext(connectionString);
            if (context.Schema != database.Schema)
            {
                throw new InvalidOperationException(
                    $"{database.ContextType.Name} uses schema '{context.Schema}' but was registered for '{database.Schema}'.");
            }

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            await context.Database.MigrateAsync(cancellationToken);
            await RevokeHistoryTableAccessAsync(context, cancellationToken);
            LogMigrated(database.Schema, pending.Count);
        }

        foreach (var step in steps)
        {
            var changed = await step.RunAsync(cancellationToken);
            LogStepApplied(step.Name, changed);
        }
    }

    // EF creates the history table outside the migrations, so the default grants from 01-roles.sh give the runtime
    // roles DML on it; no migration can take that back. Repeating the revoke on every run is harmless.
    private static async Task RevokeHistoryTableAccessAsync(ModuleDbContext context, CancellationToken cancellationToken)
    {
        var table = TableAccessMigrationExtensions.QualifiedName(context.Schema, ModuleDbContextOptions.MigrationsHistoryTable);
        var sql = $"REVOKE ALL ON {table} FROM {DatabaseRoles.RuntimeRoles}"; // Identifiers validated; roles are constants.
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Schema {Schema}: applied {Count} migration(s)")]
    private partial void LogMigrated(string schema, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Post-migration step {Step}: changed {Count} row(s)")]
    private partial void LogStepApplied(string step, int count);
}
