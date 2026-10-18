using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListLanguages;

/// <summary>An ISO 639-1 language: two-letter <c>code</c> (<c>sw</c>) and English name.</summary>
public sealed record LanguageResponse(string Code, string Name);

public sealed record ListLanguagesResponse(IReadOnlyList<LanguageResponse> Items);

/// <summary>Active languages, ordered by code.</summary>
public sealed class ListLanguagesHandler(PlatformDbContext context)
{
    public async Task<ListLanguagesResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var languages = await context.Set<Language>().AsNoTracking()
            .Where(l => l.IsActive)
            .Select(l => new LanguageResponse(l.Code, l.Name))
            .ToListAsync(cancellationToken);
        return new(languages.OrderBy(l => l.Code, StringComparer.Ordinal).ToList());
    }
}
