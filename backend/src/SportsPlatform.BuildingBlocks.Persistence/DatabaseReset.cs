using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Development's <c>seed --reset</c>: drops every module schema (and any other schema a module maps to, like
/// Platform's <c>audit</c>) as their owner, then applies the migrations again. Dropping, not truncating, because the
/// append-only triggers reject TRUNCATE for the owner too. The drops are one transaction. Two independent checks,
/// whoever calls it: the environment is Development, and the <c>Migrator</c> connection points at a local server
/// (localhost, a loopback address, a Unix socket or the compose service <c>postgres</c>), so Development settings with
/// another environment's credentials exported can't wipe that database. A non-local Development database needs
/// <c>Seeding:AllowResetOf</c> set to its host.
/// </summary>
public sealed partial class DatabaseReset(
    IEnumerable<ModuleDatabase> databases,
    MigrationRunner migrationRunner,
    IConfiguration configuration,
    SeedEnvironment environment,
    ILogger<DatabaseReset> logger)
{
    public const string AllowResetOfKey = "Seeding:AllowResetOf";

    private static readonly string[] _localHosts = ["localhost", "127.0.0.1", "::1", "postgres"];

    /// <summary>Why a reset is refused here, or null when it may run. <see cref="ResetAsync"/> checks it again.</summary>
    public string? Refusal()
    {
        if (!string.Equals(environment.Name, SeedEnvironmentPolicy.Development, StringComparison.Ordinal))
        {
            return $"A database reset is allowed only in Development (environment: {environment.Name}).";
        }

        var connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
            configuration, ConnectionStringNames.Migrator);
        var allowed = configuration[AllowResetOfKey];
        var hosts = Hosts(new NpgsqlConnectionStringBuilder(connectionString).Host);
        var remote = hosts.FirstOrDefault(host => !IsLocal(host) && !string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase));
        if (hosts.Count == 0 || remote is not null)
        {
            return $"A database reset drops only a local database; the Migrator connection points at '{remote ?? "(no host)"}'. " +
                $"For a non-local Development database, set {AllowResetOfKey} to its host.";
        }

        return null;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Refusal() is { } refusal)
        {
            throw new InvalidOperationException(refusal);
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

    // Npgsql's Host is a comma-separated list of host[:port]; an IPv6 address may be bracketed.
    private static List<string> Hosts(string? value) => [.. (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(StripPort)];

    private static string StripPort(string host)
    {
        if (host.StartsWith('['))
        {
            var end = host.IndexOf(']', StringComparison.Ordinal);
            return end > 0 ? host[1..end] : host;
        }

        // One colon: host:port. More than one: a bare IPv6 address.
        var colon = host.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && colon == host.LastIndexOf(':') ? host[..colon] : host;
    }

    private static bool IsLocal(string host) =>
        host.StartsWith('/') || _localHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    private static bool IsSystemSchema(string schema) =>
        schema is "public" or "information_schema" || schema.StartsWith("pg_", StringComparison.Ordinal);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reset: dropped schema {Schema}")]
    private partial void LogDropped(string schema);
}
