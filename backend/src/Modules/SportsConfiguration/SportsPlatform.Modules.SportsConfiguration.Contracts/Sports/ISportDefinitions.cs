namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

/// <summary>
/// The published sport definitions (S05 D5), read from the seeded rows and kept in memory until restart: configuration
/// never changes while the application runs. Variants are data: never branch on their codes (AR-11).
/// </summary>
public interface ISportDefinitions
{
    /// <summary>Every published variant at its latest version, by name, then variant code.</summary>
    Task<IReadOnlyList<SportVariant>> ListAsync(CancellationToken cancellationToken);

    /// <summary>A variant's definition at <paramref name="version"/> (the latest when null); null when there is none.</summary>
    Task<SportDefinition?> GetAsync(string variant, int? version, CancellationToken cancellationToken);
}

/// <summary>A published variant at its latest version.</summary>
public sealed record SportVariant(
    string Variant,
    string Sport,
    string Name,
    string ScoringFamily,
    ContestShape Shape,
    int LatestVersion);
