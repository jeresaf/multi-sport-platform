using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;

namespace SportsPlatform.Modules.Platform.Domain.Seeding;

/// <summary>
/// One row of <c>platform.seed_runs</c>: a seeder that ran to completion, with the checksum of what it wrote.
/// Append-only; written by the seed runner as the owner.
/// </summary>
public sealed class SeedRun : Entity
{
    private SeedRun(SeedRunRecord run)
        // Increasing within the process, so the latest run of a seeder is the one with the highest ID.
        : base(SequentialIds.Next())
    {
        SeederKey = run.SeederKey;
        Category = run.Category;
        Checksum = run.Checksum;
        Environment = run.Environment;
        Inserted = run.Outcome.Inserted;
        Updated = run.Outcome.Updated;
        Deactivated = run.Outcome.Deactivated;
        StartedAt = run.StartedAt;
        CompletedAt = run.CompletedAt;
    }

    private SeedRun()
    {
    }

    public string SeederKey { get; private set; } = string.Empty;

    public SeedCategory Category { get; private set; }

    public string Checksum { get; private set; } = string.Empty;

    public string Environment { get; private set; } = string.Empty;

    public int Inserted { get; private set; }

    public int Updated { get; private set; }

    public int Deactivated { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public static SeedRun From(SeedRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new SeedRun(run);
    }
}
