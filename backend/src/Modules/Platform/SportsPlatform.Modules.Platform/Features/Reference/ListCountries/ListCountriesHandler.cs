using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Reference.ListCountries;

/// <summary>An ISO 3166-1 country: alpha-2 <c>code</c> (<c>UG</c>), alpha-3, numeric code and English name.</summary>
public sealed record CountryResponse(
    [property: PersonalData(PersonalDataClass.Public)] string Code,
    [property: PersonalData(PersonalDataClass.Public)] string Alpha3Code,
    [property: PersonalData(PersonalDataClass.Public)] string NumericCode,
    [property: PersonalData(PersonalDataClass.Public)] string Name);

public sealed record ListCountriesResponse(
    [property: PersonalData(PersonalDataClass.Public)] IReadOnlyList<CountryResponse> Items);

/// <summary>Active countries, ordered by code.</summary>
public sealed class ListCountriesHandler(PlatformDbContext context)
{
    public async Task<ListCountriesResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var countries = await context.Set<Country>().AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new CountryResponse(c.Code, c.Alpha3Code, c.NumericCode, c.Name))
            .ToListAsync(cancellationToken);
        return new(countries.OrderBy(c => c.Code, StringComparer.Ordinal).ToList());
    }
}
