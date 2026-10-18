using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Contracts.Reference;

namespace SportsPlatform.IntegrationTests.Reference;

/// <summary><c>IReferenceData</c>: other modules' checks against the real reference data (exact, active codes only).</summary>
public sealed class ReferenceDataTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    public enum Kind
    {
        Country,
        TimeZone,
        Currency,
        Language,
    }

    [Theory]
    [InlineData(Kind.Country, "UG", true)]
    [InlineData(Kind.Country, "ug", false)]
    [InlineData(Kind.Country, "XK", false)]
    [InlineData(Kind.Country, "", false)]
    [InlineData(Kind.TimeZone, "Africa/Kampala", true)]
    [InlineData(Kind.TimeZone, "africa/kampala", false)]
    [InlineData(Kind.TimeZone, "Africa/Jinja", false)]
    [InlineData(Kind.Currency, "UGX", true)]
    [InlineData(Kind.Currency, "XAU", false)]
    [InlineData(Kind.Language, "en", true)]
    [InlineData(Kind.Language, "lg", true)]
    [InlineData(Kind.Language, "EN", false)]
    [InlineData(Kind.Language, "eng", false)]
    public async Task Only_exact_active_codes_are_accepted(Kind kind, string code, bool expected) =>
        (await IsActiveAsync(kind, code)).ShouldBe(expected);

    [Theory]
    [InlineData(Kind.Country, "countries", "KE")]
    [InlineData(Kind.TimeZone, "time_zones", "Africa/Nairobi")]
    [InlineData(Kind.Currency, "currencies", "KES")]
    [InlineData(Kind.Language, "languages", "sw")]
    public async Task Inactive_entries_are_not_accepted(Kind kind, string table, string code)
    {
        (await IsActiveAsync(kind, code)).ShouldBeTrue();
        try
        {
            await ExecuteAsync(DatabaseRoles.Migrator, $"UPDATE platform.{table} SET is_active = false WHERE code = '{code}'");

            (await IsActiveAsync(kind, code)).ShouldBeFalse();
        }
        finally
        {
            await Fixture.RestoreReferenceDataAsync();
        }
    }

    private async Task<bool> IsActiveAsync(Kind kind, string code)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var reference = scope.ServiceProvider.GetRequiredService<IReferenceData>();
        return kind switch
        {
            Kind.Country => await reference.IsActiveCountryAsync(code, Ct),
            Kind.TimeZone => await reference.IsActiveTimeZoneAsync(code, Ct),
            Kind.Currency => await reference.IsActiveCurrencyAsync(code, Ct),
            _ => await reference.IsActiveLanguageAsync(code, Ct),
        };
    }
}
