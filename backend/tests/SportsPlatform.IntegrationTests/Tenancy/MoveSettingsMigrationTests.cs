using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Settings;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The migration that moves the tenant's settings to <c>tenancy.tenant_settings</c> (S02-T4a) keeps every tenant's
/// values, in both directions. It migrates the shared database down and back up; the database ends at the latest
/// migration whatever happens.
/// </summary>
public sealed class MoveSettingsMigrationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _before = "20261019045944_AddTenantDomains";

    [Fact]
    public async Task Settings_survive_down_and_up()
    {
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Provisioning, Ct);

        try
        {
            await MigrateAsync(_before);

            var tenants = await QueryAsync(
                DatabaseRoles.Migrator,
                "SELECT id, time_zone, default_locale, currency_code, public_enabled FROM tenancy.tenants ORDER BY slug",
                r => (r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetBoolean(4)));
            tenants.ShouldBe([
                (elssc, "Africa/Kampala", "en", "UGX", false),
                (kmsa, "Africa/Kampala", "en", "UGX", true),
            ]);

            // A tenant created before the migration, in the old shape.
            await ExecuteAsync(DatabaseRoles.Migrator, """
                UPDATE tenancy.tenants SET time_zone = 'Africa/Nairobi', default_locale = 'sw-KE', currency_code = 'KES'
                WHERE slug = 'elssc'
                """);
        }
        finally
        {
            await MigrateAsync(target: null);
        }

        try
        {
            (await TenantTestData.SettingsAsync(App.Services, kmsa, Ct)).Values.ShouldBe(new TenantSettingsValues(
                "Africa/Kampala", "en", "UGX", PublicEnabled: true, StartingSoonMinutes: 30, MinorRegistrationConsent.Recommended));
            var copied = await TenantTestData.SettingsAsync(App.Services, elssc, Ct);
            copied.ShouldSatisfyAllConditions(
                s => s.Id.ShouldBe(elssc, "a copied row takes its tenant's ID"),
                s => s.Values.ShouldBe(new TenantSettingsValues(
                    "Africa/Nairobi", "sw-KE", "KES", PublicEnabled: false, StartingSoonMinutes: 30, MinorRegistrationConsent.Recommended)));
            (await QueryAsync(
                DatabaseRoles.Migrator,
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'tenancy' AND table_name = 'tenants' AND column_name = 'time_zone'",
                r => r.GetInt64(0))).ShouldBe([0L]);
        }
        finally
        {
            // Down and up dropped and recreated the plan tables, which the reset keeps: seed them again for the next test.
            await Fixture.RestoreSeedDataAsync();
        }
    }

    private async Task MigrateAsync(string? target)
    {
        var database = App.Services.GetServices<ModuleDatabase>().Single(d => d.Schema == TenancyModule.SchemaName);
        await using var context = database.CreateContext(Fixture.ConnectionString(DatabaseRoles.Migrator));
        await context.GetService<IMigrator>().MigrateAsync(target, cancellationToken: Ct);
    }
}
