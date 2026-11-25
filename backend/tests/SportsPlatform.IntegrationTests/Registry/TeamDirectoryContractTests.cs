using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Registry.Contracts.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;
using AgeGroup = SportsPlatform.Modules.Registry.Contracts.Teams.AgeGroup;
using DomainAgeGroup = SportsPlatform.Modules.Registry.Domain.Teams.AgeGroup;
using DomainGender = SportsPlatform.Modules.Registry.Domain.Teams.GenderCategory;
using DomainLevel = SportsPlatform.Modules.Registry.Domain.Teams.TeamLevel;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.Teams.TeamStatus;
using GenderCategory = SportsPlatform.Modules.Registry.Contracts.Teams.GenderCategory;
using Team = SportsPlatform.Modules.Registry.Domain.Teams.Team;
using TeamCategory = SportsPlatform.Modules.Registry.Domain.Teams.TeamCategory;
using TeamLevel = SportsPlatform.Modules.Registry.Contracts.Teams.TeamLevel;
using TeamStatus = SportsPlatform.Modules.Registry.Contracts.Teams.TeamStatus;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// <see cref="ITeamDirectory"/> (S07 D18): summaries of the scope's tenant's teams, whatever their status, with the name,
/// short name and code they had on the date asked for (D8; T3 acceptance 2); unknown IDs and other tenants' teams are
/// left out.
/// </summary>
public sealed class TeamDirectoryContractTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_team_directory_answers_summaries_of_the_scopes_tenant()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var seeta = await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows SC");
        var swifts = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", shortName: "Swifts");
        var sparrows = await SaveTeamAsync(
            App.Services, kmsa, "Seeta Sparrows FC", variant: Testhoop,
            category: new TeamCategory(DomainGender.Women, DomainAgeGroup.U17, DomainLevel.Reserve), organisationId: seeta.Id,
            status: DomainStatus.Disbanded);
        var lakeshore = await SaveTeamAsync(App.Services, elssc, "Lakeshore High School FC");

        var summaries = await DirectoryAsync(kmsa, null, swifts.Id, sparrows.Id, sparrows.Id, lakeshore.Id, Guid.CreateVersion7());

        summaries.ShouldBe(
        [
            new TeamSummary(
                swifts.Id, "Ntinda Swifts FC", "Swifts", null, TestballFull, GenderCategory.Men, AgeGroup.Senior, TeamLevel.First,
                null, TeamStatus.Active, swifts.Slug, null, null),
            new TeamSummary(
                sparrows.Id, "Seeta Sparrows FC", null, null, Testhoop, GenderCategory.Women, AgeGroup.U17, TeamLevel.Reserve,
                seeta.Id, TeamStatus.Disbanded, sparrows.Slug, null, null),
        ], ignoreOrder: true);
        (await DirectoryAsync(kmsa, null)).ShouldBeEmpty();
        (await DirectoryAsync(elssc, new DateOnly(2026, 1, 1), swifts.Id, lakeshore.Id)).Select(s => s.Id).ShouldBe([lakeshore.Id]);
    }

    // T3 acceptance 2: the old name before the rename, the new one after, and the first name before name_since.
    [Fact]
    public async Task The_name_short_name_and_code_are_the_ones_the_team_had_on_the_date()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var bunga = await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC", shortName: "Breakers");
        var kisaasi = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC", shortName: "Kestrels");
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            // Named so since 2020-01-01 (SaveTeamAsync); two renames.
            var team = await context.Set<Team>().Include(t => t.Aliases).SingleAsync(t => t.Id == bunga.Id, Ct);
            var today = new DateOnly(2026, 6, 1);
            team.Rename("Bunga Lakers FC", "Lakers", "BLK", new DateOnly(2024, 3, 1), today).IsSuccess.ShouldBeTrue();
            team.Rename("Bunga Lakeside FC", null, "BLS", new DateOnly(2025, 9, 1), today).IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync(Ct);
        }

        foreach (var (onDate, expected) in new (DateOnly?, (string, string?, string?))[]
        {
            (new DateOnly(2019, 6, 1), ("Bunga Breakers FC", "Breakers", null)),
            (new DateOnly(2020, 1, 1), ("Bunga Breakers FC", "Breakers", null)),
            (new DateOnly(2024, 2, 29), ("Bunga Breakers FC", "Breakers", null)),
            (new DateOnly(2024, 3, 1), ("Bunga Lakers FC", "Lakers", "BLK")),
            (new DateOnly(2025, 8, 31), ("Bunga Lakers FC", "Lakers", "BLK")),
            (new DateOnly(2025, 9, 1), ("Bunga Lakeside FC", null, "BLS")),
            (new DateOnly(2026, 6, 1), ("Bunga Lakeside FC", null, "BLS")),
            (null, ("Bunga Lakeside FC", null, "BLS")),
        })
        {
            var summaries = await DirectoryAsync(kmsa, onDate, bunga.Id, kisaasi.Id);

            var renamed = summaries.Single(s => s.Id == bunga.Id);
            (renamed.Name, renamed.ShortName, renamed.Code).ShouldBe(expected, $"on {onDate}");
            renamed.Slug.ShouldBe(bunga.Slug);
            var never = summaries.Single(s => s.Id == kisaasi.Id);
            (never.Name, never.ShortName, never.Code).ShouldBe(("Kisaasi Kestrels FC", "Kestrels", (string?)null), $"on {onDate}");
        }
    }

    // Every domain status and category code has a contract value of the same name (the mappings are written by hand).
    [Fact]
    public async Task Every_status_and_category_code_maps_to_the_contract_value_of_its_name()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        Enum.GetNames<TeamStatus>().ShouldBe(Enum.GetNames<DomainStatus>());
        Enum.GetNames<GenderCategory>().ShouldBe(Enum.GetNames<DomainGender>());
        Enum.GetNames<AgeGroup>().ShouldBe(Enum.GetNames<DomainAgeGroup>());
        Enum.GetNames<TeamLevel>().ShouldBe(Enum.GetNames<DomainLevel>());
        var statuses = Enum.GetValues<DomainStatus>();
        var genders = Enum.GetValues<DomainGender>();
        var ageGroups = Enum.GetValues<DomainAgeGroup>();
        var levels = Enum.GetValues<DomainLevel>();

        var saved = new List<(Guid Id, string Status, string Gender, string AgeGroup, string Level)>();
        for (var i = 0; i < new[] { statuses.Length, genders.Length, ageGroups.Length, levels.Length }.Max(); i++)
        {
            var status = statuses[i % statuses.Length];
            var category = new TeamCategory(genders[i % genders.Length], ageGroups[i % ageGroups.Length], levels[i % levels.Length]);
            var team = await SaveTeamAsync(App.Services, kmsa, $"Kampala Kites FC {i + 1}", category: category, status: status);
            saved.Add((team.Id, status.ToString(), category.Gender.ToString(), category.AgeGroup.ToString(), category.Level.ToString()));
        }

        var summaries = await DirectoryAsync(kmsa, null, [.. saved.Select(s => s.Id)]);

        summaries.Select(s => (s.Id, s.Status.ToString(), s.GenderCategory.ToString(), s.AgeGroup.ToString(), s.Level.ToString()))
            .ShouldBe(saved, ignoreOrder: true);
    }

    private async Task<IReadOnlyList<TeamSummary>> DirectoryAsync(Guid tenantId, DateOnly? onDate, params Guid[] teamIds)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<ITeamDirectory>().GetSummariesAsync(teamIds, onDate, Ct);
    }
}
