using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;

namespace SportsPlatform.Host.Health;

/// <summary>Readiness: the API's own database role (<c>App</c> connection) can run a query.</summary>
public sealed partial class DatabaseHealthCheck(IConfiguration configuration, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringNames.App);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringNames.App}' is not configured.");
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException exception)
        {
            // The check's timeout, or the probe went away: not evidence that the database is down.
            LogCheckCancelled(logger, exception);
            return HealthCheckResult.Unhealthy();
        }
#pragma warning disable CA1031 // Any failure means "not ready"; the details go to the log, never the response.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogDatabaseUnavailable(logger, exception);
            return HealthCheckResult.Unhealthy();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Readiness check timed out or was cancelled")]
    private static partial void LogCheckCancelled(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Readiness check failed: the database is unavailable")]
    private static partial void LogDatabaseUnavailable(ILogger logger, Exception exception);
}
