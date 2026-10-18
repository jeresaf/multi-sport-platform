namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>
/// Writes one set of seed data. Register with <c>services.AddSeeder&lt;T&gt;()</c> in the module's <c>AddServices</c>.
/// A run must be idempotent: running again on seeded data changes nothing. Seeded records get their IDs from
/// <c>DeterministicIds</c>. DEMO and TEST seeders go through the module's application commands (AR-22). Owner
/// contexts (REFERENCE, CONFIGURATION) have no messaging: domain events raised there are not dispatched, so those
/// seeders write plain global rows only.
/// </summary>
public interface ISeeder
{
    /// <summary>Stable and unique, e.g. <c>platform.reference.countries</c>; <c>seed_runs</c> is keyed by it.</summary>
    string Key { get; }

    SeedCategory Category { get; }

    /// <summary>Order within the category (then by <see cref="Key"/>); a seeder runs after those it depends on.</summary>
    int Order { get; }

    /// <summary>
    /// Changes whenever what the seeder writes changes (its data and its version), e.g. a SHA-256 hex digest; at most
    /// 100 characters. The runner skips a seeder whose last recorded run has the same checksum.
    /// </summary>
    string Checksum { get; }

    Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken);
}

/// <summary>How many records a run inserted, changed and deactivated (seeders never delete).</summary>
public sealed record SeedOutcome(int Inserted, int Updated, int Deactivated)
{
    public static SeedOutcome None { get; } = new(0, 0, 0);
}
