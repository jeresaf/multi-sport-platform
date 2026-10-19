using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;
using SportsPlatform.Modules.Tenancy.Seed.Demo;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The DEMO seeder's tenants (S02-T5): KMSA and ELSSC from docs/product/pilot-customer.md, through the Tenancy commands,
/// audited as <c>SYSTEM</c>, idempotent by slug.
/// </summary>
public sealed class DemoTenantSeederTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _kmsaId = DemoTenantSeeder.TenantId("kmsa");
    private static readonly Guid _elsscId = DemoTenantSeeder.TenantId("elssc");

    [Fact]
    public async Task Seeding_DEMO_creates_both_pilot_tenants_active_and_public_with_their_seed_IDs()
    {
        var report = await SeedDemoAsync(App);

        DemoSeeder(report).ShouldSatisfyAllConditions(
            s => s.Skipped.ShouldBeFalse(),
            s => s.Outcome.ShouldBe(new SeedOutcome(Inserted: 2, Updated: 0, Deactivated: 0)));
        foreach (var (id, slug, name) in new[] { (_kmsaId, "kmsa", "Kampala Metro Sports Association"), (_elsscId, "elssc", "Eastern Lakes Schools Sports Council") })
        {
            (await TenantTestData.TenantAsync(App.Services, id, Ct)).Status.ShouldBe(TenantStatus.Active);
            var settings = await TenantTestData.SettingsAsync(App.Services, id, Ct);
            settings.Values.ShouldBe(new TenantSettingsValues(
                "Africa/Kampala", "en-UG", "UGX", PublicEnabled: true, StartingSoonMinutes: 30, MinorRegistrationConsent.Required));

            var site = await App.Server.GetAsync(PublicRequests.SitePath, $"{slug}.localhost:3000");
            site.ShouldBeSite(slug);
            site.Json.GetProperty("displayName").GetString().ShouldBe(name);
        }

        (await TenantTestData.DomainsAsync(App.Services, Ct)).Select(d => d.Host).ShouldBe(["elssc.localhost", "kmsa.localhost"], ignoreOrder: true);
    }

    [Fact]
    public async Task Every_command_is_audited_with_source_SYSTEM_under_its_tenant()
    {
        await SeedDemoAsync(App);

        var entries = await ProcessedAuditEntriesAsync();

        entries.ShouldAllBe(e => e.Source == AuditSource.System && e.ActorKind == ActorKind.System && e.ActorId == SystemActor.Id
            && e.Outcome == AuditOutcome.Success);
        foreach (var id in new[] { _kmsaId, _elsscId })
        {
            entries.Where(e => e.TenantId == id).Select(e => e.ActionCode)
                .ShouldBe(["tenant.create", "tenant.activate", "tenant.settings.update"], ignoreOrder: true);
        }

        entries.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Seeding_again_changes_nothing()
    {
        await SeedDemoAsync(App);
        await ProcessedAuditEntriesAsync();
        var history = await TenantTestData.HistoryAsync(App.Services, _kmsaId, Ct);
        var version = (await TenantTestData.TenantAsync(App.Services, _kmsaId, Ct)).Version;

        var second = await SeedDemoAsync(App);
        // The checksum is unchanged, so the runner skips the seeder; run it again regardless, as a changed version would.
        await using var rerun = WithRerunSeeder();
        var third = await SeedDemoAsync(rerun);

        DemoSeeder(second).Skipped.ShouldBeTrue();
        third.Seeders.Single(s => s.Key == RerunDemoTenantSeeder.SeederKey).ShouldSatisfyAllConditions(
            s => s.Skipped.ShouldBeFalse(),
            s => s.Outcome.ShouldBe(SeedOutcome.None));
        (await TenantTestData.OutboxAsync(App.Services, Ct)).ShouldAllBe(m => m.ProcessedAt != null); // Nothing new to publish.
        (await TenantTestData.HistoryAsync(App.Services, _kmsaId, Ct)).Count.ShouldBe(history.Count);
        (await TenantTestData.TenantAsync(App.Services, _kmsaId, Ct)).Version.ShouldBe(version);
        (await ProcessedAuditEntriesAsync()).Count.ShouldBe(6);
    }

    [Fact]
    public async Task A_provisioning_demo_tenant_is_activated_and_its_settings_completed()
    {
        // A run that stopped after creating ELSSC: the seeded ID, still PROVISIONING, default consent.
        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_elsscId);
            var context = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
            const string name = "Eastern Lakes Schools Sports Council";
            context.Add(Tenant.Create("elssc", new TenantProfile(name, name, "UG"), SystemActor.Instance, App.Clock.GetUtcNow(), _elsscId));
            context.Add(TenantSettings.Create(_elsscId, TenantSettingsValues.Defaults("Africa/Kampala", "en-UG", "UGX", publicEnabled: true)));
            context.Add(TenantDomain.PlatformSubdomain(_elsscId, "elssc", "localhost"));
            await context.SaveChangesAsync(Ct);
        }

        var report = await SeedDemoAsync(App);

        DemoSeeder(report).Outcome.ShouldBe(new SeedOutcome(Inserted: 1, Updated: 1, Deactivated: 0));
        (await TenantTestData.TenantAsync(App.Services, _elsscId, Ct)).Status.ShouldBe(TenantStatus.Active);
        (await TenantTestData.SettingsAsync(App.Services, _elsscId, Ct)).MinorRegistrationConsent.ShouldBe(MinorRegistrationConsent.Required);
    }

    [Fact]
    public async Task A_suspended_demo_tenant_stays_suspended()
    {
        await SeedDemoAsync(App);
        App.Authorization.AllowAll();
        using (var client = App.CreateClient().AsUser(TenantTestData.StaffId))
        {
            using var suspended = await client.PostAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{_kmsaId}/suspend", UriKind.Relative), new { reason = "Season fees outstanding" }, Ct);
            suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using var rerun = WithRerunSeeder();
        var report = await SeedDemoAsync(rerun);

        report.Seeders.Single(s => s.Key == RerunDemoTenantSeeder.SeederKey).Outcome.ShouldBe(SeedOutcome.None);
        (await TenantTestData.TenantAsync(App.Services, _kmsaId, Ct)).Status.ShouldBe(TenantStatus.Suspended);
    }

    [Fact]
    public async Task A_demo_slug_held_by_another_tenant_stops_the_run()
    {
        var other = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => SeedDemoAsync(App));

        exception.Message.ShouldContain(other.ToString());
        (await TenantTestData.TenantCountAsync(App.Services, Ct)).ShouldBe(1);
    }

    private static SeederResult DemoSeeder(SeedReport report) => report.Seeders.Single(s => s.Key == "tenancy.demo.tenants");

    private static Task<SeedReport> SeedDemoAsync(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

    /// <summary>The test app with a copy of the seeder whose checksum never matches, so it runs every time.</summary>
    private WebApplicationFactory<Program> WithRerunSeeder() =>
        App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunDemoTenantSeeder>()));

    private async Task<List<AuditEntry>> ProcessedAuditEntriesAsync()
    {
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        return await AuditTestData.EntriesAsync(App.Services, Ct);
    }

    private sealed class RerunDemoTenantSeeder : ISeeder
    {
        public const string SeederKey = "test.demo.tenants-rerun";

        private readonly DemoTenantSeeder _seeder = new();

        public string Key => SeederKey;

        public SeedCategory Category => SeedCategory.Demo;

        public int Order => 1;

        public string Checksum { get; } = Guid.NewGuid().ToString("N");

        public Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken) => _seeder.RunAsync(context, cancellationToken);
    }
}
