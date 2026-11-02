namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;

/// <summary>
/// The published format definitions (S05 D10), read from the seeded rows and kept in memory until restart:
/// configuration never changes while the application runs.
/// </summary>
public interface IFormatDefinitions
{
    /// <summary>Every published format at its latest version, by name, then code.</summary>
    Task<IReadOnlyList<FormatDefinition>> ListAsync(CancellationToken cancellationToken);

    /// <summary>A format at <paramref name="version"/> (the latest when null); null when there is none.</summary>
    Task<FormatDefinition?> GetAsync(string code, int? version, CancellationToken cancellationToken);
}
