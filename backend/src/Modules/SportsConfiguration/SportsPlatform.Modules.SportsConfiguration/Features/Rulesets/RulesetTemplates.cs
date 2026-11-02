using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Definitions;
using SportsPlatform.Modules.SportsConfiguration.Domain.Rulesets;
using SportsPlatform.Modules.SportsConfiguration.Infrastructure;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Rulesets;

/// <summary>
/// <see cref="IRulesetTemplates"/> from <c>config.ruleset_templates</c>: every row is read and resolved once, then kept
/// in <see cref="RulesetTemplateCache"/> until restart (S05 D5). The tiebreakers come from the embedded catalogue.
/// </summary>
internal sealed class RulesetTemplates(SportsConfigurationDbContext db, RulesetTemplateCache cache, TiebreakerCatalogue catalogue)
    : IRulesetTemplates
{
    public IReadOnlyList<TiebreakerDefinition> Tiebreakers => catalogue.All;

    public async Task<IReadOnlyList<RulesetTemplate>> ListAsync(CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).Latest;

    public async Task<RulesetTemplate?> GetAsync(string code, int? version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        var published = await LoadAsync(cancellationToken);
        if (!published.Versions.TryGetValue(code, out var versions))
        {
            return null;
        }

        return version is null ? versions[^1] : versions.FirstOrDefault(template => template.Version == version);
    }

    private async Task<PublishedTemplates> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.Current is { } current)
        {
            return current;
        }

        var rows = await db.Set<PublishedRulesetTemplate>().AsNoTracking().ToListAsync(cancellationToken);
        var published = PublishedTemplates.From(rows);
        // Before the CONFIGURATION seed there is nothing to keep: the next read looks again.
        if (rows.Count > 0)
        {
            cache.Current = published;
        }

        return published;
    }
}

/// <summary>The resolved templates, shared by every scope (configuration never changes while the application runs).</summary>
internal sealed class RulesetTemplateCache
{
    private volatile PublishedTemplates? _current;

    public PublishedTemplates? Current
    {
        get => _current;
        set => _current = value;
    }
}

/// <summary>Every published template by code, in version order, and the templates at their latest version.</summary>
internal sealed record PublishedTemplates(
    IReadOnlyDictionary<string, IReadOnlyList<RulesetTemplate>> Versions,
    IReadOnlyList<RulesetTemplate> Latest)
{
    public static PublishedTemplates From(IEnumerable<PublishedRulesetTemplate> rows)
    {
        var versions = rows
            .Select(Resolve)
            .GroupBy(template => template.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RulesetTemplate>)[.. group.OrderBy(template => template.Version)],
                StringComparer.Ordinal);
        var latest = versions.Values
            .Select(list => list[^1])
            .OrderBy(template => template.Name, StringComparer.Ordinal)
            .ThenBy(template => template.Code, StringComparer.Ordinal)
            .ToList();
        return new PublishedTemplates(versions, latest);
    }

    private static RulesetTemplate Resolve(PublishedRulesetTemplate row) => RulesetTemplateContent.Read(row.Content) with
    {
        ContentHash = row.ContentHash,
        PublishedAt = row.PublishedAt,
    };
}
