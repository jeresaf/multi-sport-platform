using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Formats;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Formats;
using SportsPlatform.Modules.SportsConfiguration.Infrastructure;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats;

/// <summary>
/// <see cref="IFormatDefinitions"/> from <c>config.format_definitions</c>: every row is read and resolved once, then kept
/// in <see cref="FormatDefinitionCache"/> until restart (S05 D5).
/// </summary>
internal sealed class FormatDefinitions(SportsConfigurationDbContext db, FormatDefinitionCache cache) : IFormatDefinitions
{
    public async Task<IReadOnlyList<FormatDefinition>> ListAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).Latest;

    public async Task<FormatDefinition?> GetAsync(string code, int? version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        var published = await LoadAsync(cancellationToken);
        if (!published.Versions.TryGetValue(code, out var versions))
        {
            return null;
        }

        return version is null ? versions[^1] : versions.FirstOrDefault(format => format.Version == version);
    }

    private async Task<PublishedFormats> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.Current is { } current)
        {
            return current;
        }

        var rows = await db.Set<PublishedFormatDefinition>().AsNoTracking().ToListAsync(cancellationToken);
        var published = PublishedFormats.From(rows);
        // Before the CONFIGURATION seed there is nothing to keep: the next read looks again.
        if (rows.Count > 0)
        {
            cache.Current = published;
        }

        return published;
    }
}

/// <summary>The resolved formats, shared by every scope (configuration never changes while the application runs).</summary>
internal sealed class FormatDefinitionCache
{
    private volatile PublishedFormats? _current;

    public PublishedFormats? Current
    {
        get => _current;
        set => _current = value;
    }
}

/// <summary>Every published format by code, in version order, and the formats at their latest version.</summary>
internal sealed record PublishedFormats(
    IReadOnlyDictionary<string, IReadOnlyList<FormatDefinition>> Versions,
    IReadOnlyList<FormatDefinition> Latest)
{
    public static PublishedFormats From(IEnumerable<PublishedFormatDefinition> rows)
    {
        var versions = rows
            .Select(Resolve)
            .GroupBy(format => format.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FormatDefinition>)[.. group.OrderBy(format => format.Version)],
                StringComparer.Ordinal);
        var latest = versions.Values
            .Select(list => list[^1])
            .OrderBy(format => format.Name, StringComparer.Ordinal)
            .ThenBy(format => format.Code, StringComparer.Ordinal)
            .ToList();
        return new PublishedFormats(versions, latest);
    }

    private static FormatDefinition Resolve(PublishedFormatDefinition row) => FormatDefinitionContent.Read(row.Content) with
    {
        ContentHash = row.ContentHash,
        PublishedAt = row.PublishedAt,
    };
}
