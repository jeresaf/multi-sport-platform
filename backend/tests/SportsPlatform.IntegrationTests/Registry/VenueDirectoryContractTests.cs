using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Registry.Contracts.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.Venues.VenueStatus;
using VenueStatus = SportsPlatform.Modules.Registry.Contracts.Venues.VenueStatus;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// <see cref="IVenueDirectory"/> (S07 D18): summaries of the scope's tenant's venues, whatever their status; unknown IDs
/// and other tenants' venues are left out.
/// </summary>
public sealed class VenueDirectoryContractTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_venue_directory_answers_summaries_of_the_scopes_tenant()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var bukoto = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", supportedVariants: [TestSportDefinitions.TestballFull]);
        var kisaasi = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground", status: DomainStatus.Closed);
        var seeta = await SaveVenueAsync(App.Services, elssc, "Seeta Hill Sports Field");

        var summaries = await DirectoryAsync(kmsa, bukoto.Id, kisaasi.Id, kisaasi.Id, seeta.Id, Guid.CreateVersion7());

        summaries.Select(s => (s.Id, s.Name, s.ShortName, s.TimeZone, s.City, s.Country, string.Join(',', s.SupportedVariants), s.Status)).ShouldBe(
        [
            (bukoto.Id, "Bukoto Community Stadium", (string?)null, "Africa/Kampala", "Kampala", "UG", TestSportDefinitions.TestballFull, VenueStatus.Active),
            (kisaasi.Id, "Kisaasi Recreation Ground", (string?)null, "Africa/Kampala", "Kampala", "UG", "", VenueStatus.Closed),
        ], ignoreOrder: true);
        (await DirectoryAsync(kmsa)).ShouldBeEmpty();
        (await DirectoryAsync(elssc, bukoto.Id, seeta.Id)).Select(s => s.Id).ShouldBe([seeta.Id]);
    }

    // Every domain status has a contract status of the same name (the mapping is written by hand).
    [Theory]
    [InlineData(DomainStatus.Active)]
    [InlineData(DomainStatus.TemporarilyUnavailable)]
    [InlineData(DomainStatus.Closed)]
    [InlineData(DomainStatus.Archived)]
    public async Task Every_status_maps_to_the_contract_status_of_its_name(DomainStatus status)
    {
        Enum.GetValues<DomainStatus>().Length.ShouldBe(4, "a new venue status needs a row here and a contract value");
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", status: status);

        var summary = (await DirectoryAsync(kmsa, venue.Id)).ShouldHaveSingleItem();

        summary.Status.ToString().ShouldBe(status.ToString());
    }

    private async Task<IReadOnlyList<VenueSummary>> DirectoryAsync(Guid tenantId, params Guid[] venueIds)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IVenueDirectory>().GetSummariesAsync(venueIds, Ct);
    }
}
