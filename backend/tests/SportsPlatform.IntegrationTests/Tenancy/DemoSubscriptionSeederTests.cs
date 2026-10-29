using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Infrastructure;
using SportsPlatform.Modules.Tenancy.Seed.Demo;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// The DEMO seeder's subscriptions (S04 D18, pilot-customer.md): KMSA complimentary for six months, ELSSC paid for 45
/// days, both on STANDARD from the seed date, through <c>subscription.assign</c> as <c>SYSTEM</c>; ELSSC's event pass
/// allows one active edition until its period ends (<c>entitlement.override</c>). A tenant with a period (or an override)
/// is left alone.
/// </summary>
public sealed class DemoSubscriptionSeederTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _kmsaId = DemoTenantSeeder.TenantId("kmsa");
    private static readonly Guid _elsscId = DemoTenantSeeder.TenantId("elssc");

    private DateOnly Today => TenantCalendar.Today(App.Clock, "Africa/Kampala");

    [Fact]
    public async Task Seeding_DEMO_gives_both_pilot_tenants_a_STANDARD_period_from_today_and_full_access()
    {
        var report = await SeedDemoAsync(App);

        report.Seeders.Single(s => s.Key == "tenancy.demo.subscriptions").Outcome.ShouldBe(new SeedOutcome(Inserted: 3, Updated: 0, Deactivated: 0));
        var kmsa = (await PeriodsAsync(_kmsaId)).ShouldHaveSingleItem();
        (kmsa.PlanCode, kmsa.Kind, kmsa.StartsOn, kmsa.EndsOn).ShouldBe(("STANDARD", SubscriptionKind.Complimentary, Today, Today.AddMonths(6).AddDays(-1)));
        var elssc = (await PeriodsAsync(_elsscId)).ShouldHaveSingleItem();
        (elssc.PlanCode, elssc.Kind, elssc.StartsOn, elssc.EndsOn).ShouldBe(("STANDARD", SubscriptionKind.Paid, Today, Today.AddDays(44)));
        foreach (var period in new[] { kmsa, elssc })
        {
            (period.ExpiringWindowDays, period.GraceDays).ShouldBe((30, 14));
            period.IsCancelled.ShouldBeFalse();
        }

        (await AccessAsync(_kmsaId)).Mode.ShouldBe(TenantAccessMode.Full);
        (await AccessAsync(_elsscId)).Mode.ShouldBe(TenantAccessMode.Full);
    }

    [Fact]
    public async Task ELSSCs_event_pass_allows_one_active_edition_until_its_period_ends()
    {
        await SeedDemoAsync(App);

        (await OverridesAsync(_kmsaId)).ShouldBeEmpty();
        var eventPass = (await OverridesAsync(_elsscId)).ShouldHaveSingleItem();
        (eventPass.EntitlementCode, eventPass.Value, eventPass.EffectiveUntil, eventPass.IsRemoved)
            .ShouldBe(("MAX_ACTIVE_EDITIONS", EntitlementValue.Quota(1), Today.AddDays(44), false));
        var entry = (await AuditEntriesAsync("entitlement.override")).ShouldHaveSingleItem();
        (entry.TenantId, entry.ActorKind, entry.Source, entry.EntityId, entry.Reason)
            .ShouldBe((_elsscId, ActorKind.System, AuditSource.System, eventPass.Id, eventPass.Reason));
    }

    [Fact]
    public async Task Each_period_is_assigned_by_the_system_under_its_tenant()
    {
        await SeedDemoAsync(App);

        var entries = await SubscriptionAuditEntriesAsync();

        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(e => e.ActionCode == "subscription.assign" && e.Source == AuditSource.System && e.ActorKind == ActorKind.System
            && e.ActorId == SystemActor.Id && e.Outcome == AuditOutcome.Success && e.EntityType == "SubscriptionPeriod");
        entries.Select(e => e.TenantId).ShouldBe([_kmsaId, _elsscId], ignoreOrder: true);
    }

    [Fact]
    public async Task A_tenant_that_has_a_period_is_left_alone()
    {
        await SeedDemoAsync(App);
        await SubscriptionAuditEntriesAsync();

        // The checksum is unchanged, so the runner skips the seeder; run it again regardless, as a changed version would.
        await using var rerun = App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunSeeder>()));
        var report = await SeedDemoAsync(rerun);

        report.Seeders.Single(s => s.Key == RerunSeeder.SeederKey).Outcome.ShouldBe(SeedOutcome.None);
        (await PeriodsAsync(_kmsaId)).Count.ShouldBe(1);
        (await PeriodsAsync(_elsscId)).Count.ShouldBe(1);
        (await OverridesAsync(_elsscId)).Count.ShouldBe(1);
        (await SubscriptionAuditEntriesAsync()).Count.ShouldBe(2);
        (await AuditEntriesAsync("entitlement.override")).Count.ShouldBe(1);
    }

    private static Task<SeedReport> SeedDemoAsync(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

    /// <summary>The tenant's periods, read under that tenant (they are tenant-owned).</summary>
    private async Task<List<SubscriptionPeriod>> PeriodsAsync(Guid tenantId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<SubscriptionPeriod>().AsNoTracking().ToListAsync(Ct);
    }

    private async Task<List<EntitlementOverride>> OverridesAsync(Guid tenantId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<EntitlementOverride>().AsNoTracking().ToListAsync(Ct);
    }

    private async Task<TenantAccess> AccessAsync(Guid tenantId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return (await scope.ServiceProvider.GetRequiredService<ITenantAccess>().GetAccessAsync(tenantId, Ct)).ShouldNotBeNull();
    }

    private Task<List<AuditEntry>> SubscriptionAuditEntriesAsync() => AuditEntriesAsync("subscription.");

    private async Task<List<AuditEntry>> AuditEntriesAsync(string actionPrefix)
    {
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        return [.. (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode.StartsWith(actionPrefix, StringComparison.Ordinal))];
    }

    private sealed class RerunSeeder : ISeeder
    {
        public const string SeederKey = "test.demo.subscriptions-rerun";

        private readonly DemoSubscriptionSeeder _seeder = new();

        public string Key => SeederKey;

        public SeedCategory Category => SeedCategory.Demo;

        public int Order => 6;

        public string Checksum { get; } = Guid.NewGuid().ToString("N");

        public Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken) => _seeder.RunAsync(context, cancellationToken);
    }
}
