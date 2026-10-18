using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListCurrencies;

/// <summary>An ISO 4217 currency: alphabetic <c>code</c> (<c>UGX</c>), numeric code, English name, minor units (UGX 0).</summary>
public sealed record CurrencyResponse(string Code, string NumericCode, string Name, int MinorUnits);

public sealed record ListCurrenciesResponse(IReadOnlyList<CurrencyResponse> Items);

/// <summary>Active currencies, ordered by code.</summary>
public sealed class ListCurrenciesHandler(PlatformDbContext context)
{
    public async Task<ListCurrenciesResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var currencies = await context.Set<Currency>().AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new CurrencyResponse(c.Code, c.NumericCode, c.Name, c.MinorUnits))
            .ToListAsync(cancellationToken);
        return new(currencies.OrderBy(c => c.Code, StringComparer.Ordinal).ToList());
    }
}
