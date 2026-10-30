using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Sports;
using SportsPlatform.Modules.SportsConfiguration.Infrastructure;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports;

/// <summary>
/// <see cref="ISportDefinitions"/> from <c>config.sport_definitions</c>: every row is read and resolved once, then kept
/// in <see cref="SportDefinitionCache"/> until restart (S05 D5).
/// </summary>
internal sealed class SportDefinitions(SportsConfigurationDbContext db, SportDefinitionCache cache) : ISportDefinitions
{
    public async Task<IReadOnlyList<SportVariant>> ListAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).Variants;

    public async Task<SportDefinition?> GetAsync(string variant, int? version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variant);
        var published = await LoadAsync(cancellationToken);
        if (!published.Versions.TryGetValue(variant, out var versions))
        {
            return null;
        }

        return version is null ? versions[^1] : versions.FirstOrDefault(definition => definition.Version == version);
    }

    private async Task<PublishedSports> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.Current is { } current)
        {
            return current;
        }

        var rows = await db.Set<PublishedSportDefinition>().AsNoTracking().ToListAsync(cancellationToken);
        var published = PublishedSports.From(rows);
        // Before the CONFIGURATION seed there is nothing to keep: the next read looks again.
        if (rows.Count > 0)
        {
            cache.Current = published;
        }

        return published;
    }
}

/// <summary>The resolved definitions, shared by every scope (configuration never changes while the application runs).</summary>
internal sealed class SportDefinitionCache
{
    private volatile PublishedSports? _current;

    public PublishedSports? Current
    {
        get => _current;
        set => _current = value;
    }
}

/// <summary>Every published definition by variant, in version order, and the variants at their latest version.</summary>
internal sealed record PublishedSports(
    IReadOnlyDictionary<string, IReadOnlyList<SportDefinition>> Versions,
    IReadOnlyList<SportVariant> Variants)
{
    public static PublishedSports From(IEnumerable<PublishedSportDefinition> rows)
    {
        var versions = rows
            .Select(Resolve)
            .GroupBy(definition => definition.Variant, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SportDefinition>)[.. group.OrderBy(definition => definition.Version)],
                StringComparer.Ordinal);
        var variants = versions.Values
            .Select(list => list[^1])
            .Select(latest => new SportVariant(latest.Variant, latest.Sport, latest.Name, latest.ScoringFamily, latest.Shape, latest.Version))
            .OrderBy(variant => variant.Name, StringComparer.Ordinal)
            .ThenBy(variant => variant.Variant, StringComparer.Ordinal)
            .ToList();
        return new PublishedSports(versions, variants);
    }

    private static SportDefinition Resolve(PublishedSportDefinition row) => SportDefinitionContent.Read(row.Content) with
    {
        GenericEventTypes = GenericEventTypes.All,
        ContentHash = row.ContentHash,
        PublishedAt = row.PublishedAt,
    };
}
