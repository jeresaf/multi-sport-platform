using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Registry;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The migration that adds the venue directory's sort key (S07-T1b-2) gives venues saved before it the key C# would,
/// in every tenant, through forced RLS. It migrates Registry down and back up; the database ends at the latest
/// migration whatever happens.
/// </summary>
public sealed class VenueDirectoryMigrationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _before = "20261121063904_AddVenues";

    [Fact]
    public async Task Venues_saved_before_the_migration_get_the_sort_key_csharp_gives()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        string[] kmsaNames = ["Bukoto Community Stadium", "  ÉNSIBUKO   Grounds ", "Ggaba-Lakeside Rugby Ground", "St. Mbaaga's Field", "Naguru Ñ Arena", "ስታዲየም 2"];
        foreach (var name in kmsaNames)
        {
            await SaveVenueAsync(App.Services, kmsa, name);
        }

        await SaveVenueAsync(App.Services, elssc, "Lakeshore High School Playing Fields");

        try
        {
            await MigrateAsync(_before);
            (await QueryAsync(
                DatabaseRoles.Migrator,
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'registry' AND table_name = 'venues' AND column_name = 'sort_key'",
                r => r.GetInt64(0))).ShouldBe([0L]);
        }
        finally
        {
            await MigrateAsync(target: null);
        }

        var rows = await QueryAsync(
            DatabaseRoles.Platform, "SELECT name, sort_key FROM registry.venues ORDER BY sort_key, id", r => (r.GetString(0), r.GetString(1)));
        rows.Count.ShouldBe(kmsaNames.Length + 1);
        rows.ShouldAllBe(row => row.Item2 == Venue.SortKeyOf(row.Item1));
        (await QueryAsync(
            DatabaseRoles.Migrator,
            "SELECT relforcerowsecurity FROM pg_class WHERE oid = 'registry.venues'::regclass",
            r => r.GetBoolean(0))).ShouldBe([true]);
    }

    private async Task MigrateAsync(string? target)
    {
        var database = App.Services.GetServices<ModuleDatabase>().Single(d => d.Schema == RegistryModule.SchemaName);
        await using var context = database.CreateContext(Fixture.ConnectionString(DatabaseRoles.Migrator));
        await context.GetService<IMigrator>().MigrateAsync(target, cancellationToken: Ct);
    }
}
