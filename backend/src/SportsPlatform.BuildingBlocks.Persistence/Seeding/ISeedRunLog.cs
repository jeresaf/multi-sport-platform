namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>The seed bookkeeping (<c>platform.seed_runs</c>, owned by the Platform module).</summary>
public interface ISeedRunLog
{
    /// <summary>The checksum of the seeder's most recent recorded run, or null if it never ran.</summary>
    Task<string?> LastChecksumAsync(string seederKey, CancellationToken cancellationToken);

    Task AppendAsync(SeedRunRecord run, CancellationToken cancellationToken);
}

/// <summary>A completed seeder run. Skipped and failed runs are not recorded.</summary>
public sealed record SeedRunRecord(
    string SeederKey,
    SeedCategory Category,
    string Checksum,
    string Environment,
    SeedOutcome Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
