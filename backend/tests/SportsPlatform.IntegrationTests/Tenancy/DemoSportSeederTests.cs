using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy.Domain.Sports;
using SportsPlatform.Modules.Tenancy.Infrastructure;
using SportsPlatform.Modules.Tenancy.Seed.Demo;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The DEMO sports (S06 D23, S05 D19): football enabled for KMSA and ELSSC through <c>tenant.sport.enable</c> as
/// <c>SYSTEM</c>; a variant already enabled is skipped.
/// </summary>
public sealed class DemoSportSeederTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _football = "FOOTBALL_ASSOCIATION";

    private static readonly Guid _kmsaId = DemoTenantSeeder.TenantId("kmsa");
    private static readonly Guid _elsscId = DemoTenantSeeder.TenantId("elssc");

    [Fact]
    public async Task Seeding_DEMO_enables_football_for_both_pilot_tenants_as_the_system()
    {
        var report = await SeedDemoAsync(App);

        report.Seeders.Single(s => s.Key == "tenancy.demo.sports").Outcome.ShouldBe(new SeedOutcome(Inserted: 2, Updated: 0, Deactivated: 0));
        foreach (var tenantId in new[] { _kmsaId, _elsscId })
        {
            var sport = (await SportsAsync(tenantId)).ShouldHaveSingleItem();
            (sport.Variant, sport.DisabledAt, sport.EnabledBy).ShouldBe((_football, (DateTimeOffset?)null, SystemActor.Id));
        }

        var entries = await EnableAuditEntriesAsync();
        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(e => e.Source == AuditSource.System && e.ActorKind == ActorKind.System && e.Outcome == AuditOutcome.Success);
        entries.Select(e => e.TenantId).ShouldBe([_kmsaId, _elsscId], ignoreOrder: true);
    }

    [Fact]
    public async Task An_enabled_variant_is_left_alone()
    {
        await SeedDemoAsync(App);

        await using var rerun = App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunSeeder>()));
        var report = await SeedDemoAsync(rerun);

        report.Seeders.Single(s => s.Key == RerunSeeder.SeederKey).Outcome.ShouldBe(SeedOutcome.None);
        (await SportsAsync(_kmsaId)).Count.ShouldBe(1);
        (await EnableAuditEntriesAsync()).Count.ShouldBe(2);
    }

    private static Task<SeedReport> SeedDemoAsync(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

    private async Task<List<TenantSport>> SportsAsync(Guid tenantId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<TenantSport>().AsNoTracking().ToListAsync(Ct);
    }

    private async Task<List<AuditEntry>> EnableAuditEntriesAsync()
    {
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        return [.. (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "tenant.sport.enable")];
    }

    private sealed class RerunSeeder : ISeeder
    {
        public const string SeederKey = "test.demo.sports-rerun";

        private readonly DemoSportSeeder _seeder = new();

        public string Key => SeederKey;

        public SeedCategory Category => SeedCategory.Demo;

        public int Order => 7;

        public string Checksum { get; } = Guid.NewGuid().ToString("N");

        public Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken) => _seeder.RunAsync(context, cancellationToken);
    }
}
