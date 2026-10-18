using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Platform.Domain.Reference;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Reference;

/// <summary><see cref="IReferenceData"/> on the reference tables (unique by code, so each check is one index lookup).</summary>
internal sealed class ReferenceData(PlatformDbContext context) : IReferenceData
{
    public Task<bool> IsActiveCountryAsync(string code, CancellationToken cancellationToken) =>
        IsActiveAsync<Country>(code, cancellationToken);

    public Task<bool> IsActiveTimeZoneAsync(string code, CancellationToken cancellationToken) =>
        IsActiveAsync<IanaTimeZone>(code, cancellationToken);

    public Task<bool> IsActiveCurrencyAsync(string code, CancellationToken cancellationToken) =>
        IsActiveAsync<Currency>(code, cancellationToken);

    public Task<bool> IsActiveLanguageAsync(string code, CancellationToken cancellationToken) =>
        IsActiveAsync<Language>(code, cancellationToken);

    private Task<bool> IsActiveAsync<TEntry>(string code, CancellationToken cancellationToken)
        where TEntry : ReferenceEntry
    {
        ArgumentNullException.ThrowIfNull(code);
        return context.Set<TEntry>().AsNoTracking().AnyAsync(e => e.Code == code && e.IsActive, cancellationToken);
    }
}
