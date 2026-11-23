using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Registry.Contracts.Organisations;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.Organisations.OrganisationStatus;
using DomainType = SportsPlatform.Modules.Registry.Domain.Organisations.OrganisationType;
using OrganisationStatus = SportsPlatform.Modules.Registry.Contracts.Organisations.OrganisationStatus;
using OrganisationType = SportsPlatform.Modules.Registry.Contracts.Organisations.OrganisationType;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// <see cref="IOrganisationDirectory"/> (S07 D18): summaries of the scope's tenant's organisations, whatever their
/// status; unknown IDs and other tenants' organisations are left out.
/// </summary>
public sealed class OrganisationDirectoryContractTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_organisation_directory_answers_summaries_of_the_scopes_tenant()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", shortName: "Swifts");
        var kisaasi = await SaveOrganisationAsync(
            App.Services, kmsa, "Kisaasi Kestrels SC", type: DomainType.Association, status: DomainStatus.Dissolved);
        var lakeshore = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", type: DomainType.School);

        var summaries = await DirectoryAsync(kmsa, ntinda.Id, kisaasi.Id, kisaasi.Id, lakeshore.Id, Guid.CreateVersion7());

        summaries.Select(s => (s.Id, s.DisplayName, s.ShortName, s.Type, s.Status, s.LogoAssetId)).ShouldBe(
        [
            (ntinda.Id, "Ntinda Swifts SC", "Swifts", OrganisationType.Club, OrganisationStatus.Active, (Guid?)null),
            (kisaasi.Id, "Kisaasi Kestrels SC", (string?)null, OrganisationType.Association, OrganisationStatus.Dissolved, (Guid?)null),
        ], ignoreOrder: true);
        (await DirectoryAsync(kmsa)).ShouldBeEmpty();
        (await DirectoryAsync(elssc, ntinda.Id, lakeshore.Id)).Select(s => s.Id).ShouldBe([lakeshore.Id]);
    }

    // Every domain status and type has a contract value of the same name (the mappings are written by hand).
    [Fact]
    public async Task Every_status_and_type_maps_to_the_contract_value_of_its_name()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var statuses = Enum.GetValues<DomainStatus>();
        var types = Enum.GetValues<DomainType>();
        Enum.GetNames<OrganisationStatus>().ShouldBe(Enum.GetNames<DomainStatus>());
        Enum.GetNames<OrganisationType>().ShouldBe(Enum.GetNames<DomainType>());

        var saved = new List<(Guid Id, string Status, string Type)>();
        for (var i = 0; i < Math.Max(statuses.Length, types.Length); i++)
        {
            var status = statuses[i % statuses.Length];
            var type = types[i % types.Length];
            var organisation = await SaveOrganisationAsync(App.Services, kmsa, $"Kampala Club {i + 1}", type: type, status: status);
            saved.Add((organisation.Id, status.ToString(), type.ToString()));
        }

        var summaries = await DirectoryAsync(kmsa, [.. saved.Select(s => s.Id)]);

        summaries.Select(s => (s.Id, s.Status.ToString(), s.Type.ToString())).ShouldBe(saved, ignoreOrder: true);
    }

    private async Task<IReadOnlyList<OrganisationSummary>> DirectoryAsync(Guid tenantId, params Guid[] organisationIds)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IOrganisationDirectory>().GetSummariesAsync(organisationIds, Ct);
    }
}
