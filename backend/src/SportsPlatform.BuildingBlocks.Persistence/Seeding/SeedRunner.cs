using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>
/// Runs the registered seeders of a category and its prerequisites, in category order, then by
/// <see cref="ISeeder.Order"/> and <see cref="ISeeder.Key"/>. Each seeder gets its own scope; a seeder whose
/// checksum matches its last recorded run is skipped. A seeder that throws stops the run and records nothing. One run
/// at a time per database: a run holds a PostgreSQL advisory lock, and a second one is refused.
/// </summary>
public sealed partial class SeedRunner(
    IServiceScopeFactory scopeFactory,
    OwnerContextFactory ownerContexts,
    DatabaseReset databaseReset,
    SeedEnvironment environment,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<SeedRunner> logger)
{
    /// <summary>The longest <see cref="ISeeder.Key"/> and <see cref="ISeeder.Checksum"/> that <c>seed_runs</c> stores.</summary>
    public const int MaxKeyLength = 100;

    /// <inheritdoc cref="MaxKeyLength"/>
    public const int MaxChecksumLength = 100;

    /// <summary>The advisory lock key of a seed run (any fixed bigint, unique to seeding).</summary>
    public const long LockKey = 0x5345_4544_5255_4E; // "SEEDRUN"

    public async Task<SeedReport> RunAsync(SeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Before anything touches the database.
        if (SeedEnvironmentPolicy.Refusal(request, environment.Name) is { } refusal)
        {
            LogRefused(refusal);
            return SeedReport.Refused(refusal);
        }

        if (request.Reset && databaseReset.Refusal() is { } resetRefusal)
        {
            LogRefused(resetRefusal);
            return SeedReport.Refused(resetRefusal);
        }

        // Session-level, on a connection held for the whole run; unlocked explicitly, since a pooled connection outlives Close.
        await using var lockConnection = new NpgsqlConnection(
            PersistenceServiceCollectionExtensions.RequiredConnectionString(configuration, ConnectionStringNames.Migrator));
        await lockConnection.OpenAsync(cancellationToken);
        if (!await AdvisoryLockAsync(lockConnection, "pg_try_advisory_lock", cancellationToken))
        {
            const string busy = "Another seed run is in progress on this database; try again when it has finished.";
            LogRefused(busy);
            return SeedReport.Refused(busy);
        }

        try
        {
            return await RunLockedAsync(request, cancellationToken);
        }
        finally
        {
            await AdvisoryLockAsync(lockConnection, "pg_advisory_unlock", CancellationToken.None);
        }
    }

    private async Task<SeedReport> RunLockedAsync(SeedRequest request, CancellationToken cancellationToken)
    {
        if (request.Reset)
        {
            await databaseReset.ResetAsync(cancellationToken);
        }

        var categories = request.Category.WithPrerequisites();
        List<(string Key, SeedCategory Category, int Order)> seeders;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            seeders = [.. Ordered(scope.ServiceProvider.GetServices<ISeeder>(), categories).Select(s => (s.Key, s.Category, s.Order))];
        }

        var results = new List<SeederResult>();
        foreach (var (key, category, _) in seeders)
        {
            results.Add(await RunSeederAsync(key, category, cancellationToken));
        }

        return new SeedReport(null, results);
    }

    private async Task<SeederResult> RunSeederAsync(string key, SeedCategory category, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetServices<ISeeder>().Single(s => s.Key == key);
        var runLog = scope.ServiceProvider.GetRequiredService<ISeedRunLog>();

        var checksum = seeder.Checksum;
        // Before the seeder writes anything: the run couldn't be recorded afterwards.
        if (string.IsNullOrWhiteSpace(checksum) || checksum.Length > MaxChecksumLength)
        {
            throw new InvalidOperationException($"Seeder {key} has an empty checksum or one longer than {MaxChecksumLength} characters.");
        }

        if (await runLog.LastChecksumAsync(key, cancellationToken) == checksum)
        {
            LogSkipped(key);
            return new SeederResult(key, category, Skipped: true, SeedOutcome.None);
        }

        var startedAt = timeProvider.GetUtcNow();
        SeedOutcome outcome;
        await using (var context = new SeedContext(category, scope.ServiceProvider, ownerContexts))
        {
            outcome = await seeder.RunAsync(context, cancellationToken);
        }

        await runLog.AppendAsync(
            new SeedRunRecord(key, category, checksum, environment.Name, outcome, startedAt, timeProvider.GetUtcNow()),
            cancellationToken);
        LogSeeded(key, outcome.Inserted, outcome.Updated, outcome.Deactivated);
        return new SeederResult(key, category, Skipped: false, outcome);
    }

    private static async Task<bool> AdvisoryLockAsync(NpgsqlConnection connection, string function, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT {function}(@key)", connection); // Function name is a constant.
        command.Parameters.AddWithValue("key", LockKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    internal static IEnumerable<ISeeder> Ordered(IEnumerable<ISeeder> seeders, IReadOnlyList<SeedCategory> categories)
    {
        var all = seeders.ToList();
        var badKey = all.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Key) || s.Key.Length > MaxKeyLength);
        if (badKey is not null)
        {
            throw new InvalidOperationException($"{badKey.GetType().Name} has an empty key or one longer than {MaxKeyLength} characters.");
        }

        var duplicate = all.GroupBy(s => s.Key, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Seeder key '{duplicate.Key}' is registered more than once.");
        }

        var position = categories.Select((category, index) => (category, index)).ToDictionary(p => p.category, p => p.index);
        return all
            .Where(s => position.ContainsKey(s.Category))
            .OrderBy(s => position[s.Category])
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Key, StringComparer.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seeding refused: {Refusal}")]
    private partial void LogRefused(string refusal);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeder {SeederKey}: unchanged, skipped")]
    private partial void LogSkipped(string seederKey);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Seeder {SeederKey}: {Inserted} inserted, {Updated} updated, {Deactivated} deactivated")]
    private partial void LogSeeded(string seederKey, int inserted, int updated, int deactivated);
}

/// <summary>The environment seeding runs in (the host's environment name).</summary>
public sealed record SeedEnvironment(string Name);

/// <summary>A seed run's outcome: refused (nothing ran) or the result of each seeder in run order.</summary>
public sealed record SeedReport(string? Refusal, IReadOnlyList<SeederResult> Seeders)
{
    public bool IsRefused => Refusal is not null;

    public static SeedReport Refused(string refusal) => new(refusal, []);
}

public sealed record SeederResult(string Key, SeedCategory Category, bool Skipped, SeedOutcome Outcome);
