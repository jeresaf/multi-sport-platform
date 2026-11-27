using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Seed.Demo;
using SportsPlatform.Modules.Tenancy.Seed.Demo;
using ITeamDirectory = SportsPlatform.Modules.Registry.Contracts.Teams.ITeamDirectory;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The DEMO venues, organisations and teams (S07 D22, pilot-customer.md): KMSA's five venues, twenty clubs and football
/// teams, ELSSC's venue, four schools and under-16 teams, created through the Registry's commands as <c>SYSTEM</c>;
/// idempotent by step (S07-T7 acceptance).
/// </summary>
public sealed class DemoRegistrySeederTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _kmsaId = DemoTenantSeeder.TenantId("kmsa");
    private static readonly Guid _elsscId = DemoTenantSeeder.TenantId("elssc");

    [Fact]
    public async Task Seeding_DEMO_twice_gives_KMSA_and_ELSSC_their_registry_and_changes_nothing_the_second_time()
    {
        var report = await SeedDemoAsync(App);

        report.Seeders.Single(s => s.Key == "registry.demo.venues").Outcome.ShouldBe(new SeedOutcome(Inserted: 6, Updated: 0, Deactivated: 0));
        report.Seeders.Single(s => s.Key == "registry.demo.organisations").Outcome.ShouldBe(new SeedOutcome(Inserted: 24, Updated: 0, Deactivated: 0));
        report.Seeders.Single(s => s.Key == "registry.demo.teams").Outcome.ShouldBe(new SeedOutcome(Inserted: 33, Updated: 0, Deactivated: 0));
        var kmsa = await RegistryAsync(_kmsaId);
        kmsa.Venues.Count.ShouldBe(5);
        kmsa.Organisations.Count.ShouldBe(20);
        kmsa.Teams.Count.ShouldBe(25);
        kmsa.Teams.ShouldAllBe(t => t.Variant == "FOOTBALL_ASSOCIATION");
        kmsa.Teams.Count(t => t.AgeGroup == AgeGroup.Senior && t.GenderCategory == GenderCategory.Men && t.Level == TeamLevel.First).ShouldBe(10);
        kmsa.Teams.Count(t => t.GenderCategory == GenderCategory.Women).ShouldBe(6);
        kmsa.Teams.Count(t => t.AgeGroup == AgeGroup.U17).ShouldBe(8);
        var elssc = await RegistryAsync(_elsscId);
        elssc.Venues.ShouldHaveSingleItem().Name.ShouldBe("Lakeshore High School Playing Fields");
        elssc.Organisations.Count.ShouldBe(4);
        elssc.Organisations.ShouldAllBe(o => o.Type == OrganisationType.School);
        elssc.Teams.Count.ShouldBe(8);
        elssc.Teams.ShouldAllBe(t => t.AgeGroup == AgeGroup.U16 && t.OrganisationId != null);

        // Again, with every step run: the seeders find each record and its steps done.
        var before = (await RegistryAuditEntriesAsync()).Count;
        await using var rerun = WithRerun(App);
        var unchanged = await SeedDemoAsync(rerun);

        unchanged.Seeders.Where(s => s.Key.StartsWith(RerunSeeder<DemoVenueSeeder>.KeyPrefix, StringComparison.Ordinal))
            .Select(s => s.Outcome).ShouldBe([SeedOutcome.None, SeedOutcome.None, SeedOutcome.None]);
        (await RegistryAuditEntriesAsync()).Count.ShouldBe(before);
        (await RegistryAsync(_kmsaId)).Teams.Count.ShouldBe(25);
    }

    [Fact]
    public async Task Mutundwe_Meteors_FC_was_Mutundwe_Weavers_FC_and_Kirekas_reserve_team_is_archived()
    {
        await SeedDemoAsync(App);
        var teams = (await RegistryAsync(_kmsaId)).Teams;

        var meteors = teams.Single(t => t.Name == "Mutundwe Meteors FC");
        meteors.NameSince.ShouldBe(new DateOnly(2026, 1, 1));
        meteors.Slug.ShouldBe("mutundwe-meteors-fc-senior-men");
        meteors.Code.ShouldBe("MUT");
        var former = meteors.Aliases.ShouldHaveSingleItem();
        (former.Name, former.Kind, former.ValidFrom, former.ValidTo, former.Code)
            .ShouldBe(("Mutundwe Weavers FC", AliasKind.FormerName, new DateOnly(2018, 1, 1), new DateOnly(2025, 12, 31), "MUW"));
        await using (var scope = RegistryTestData.ScopeFor(App.Services, _kmsaId, out _))
        {
            var directory = scope.ServiceProvider.GetRequiredService<ITeamDirectory>();
            (await directory.GetSummariesAsync([meteors.Id], new DateOnly(2025, 6, 1), Ct)).ShouldHaveSingleItem().Name.ShouldBe("Mutundwe Weavers FC");
            (await directory.GetSummariesAsync([meteors.Id], onDate: null, Ct)).ShouldHaveSingleItem().Name.ShouldBe("Mutundwe Meteors FC");
        }

        var reserve = teams.Single(t => t.Name == "Kireka Kingfishers FC" && t.Level == TeamLevel.Reserve);
        reserve.Status.ShouldBe(TeamStatus.Archived);
        reserve.StatusHistory.Single(c => c.ToStatus == TeamStatus.Archived).Reason.ShouldBe("Folded into the first team");
        teams.Count(t => t.Status != TeamStatus.Archived).ShouldBe(24);
    }

    [Fact]
    public async Task Clubs_run_their_teams_at_their_venues_with_placeholder_logos_and_badges()
    {
        await SeedDemoAsync(App);
        var kmsa = await RegistryAsync(_kmsaId);
        Guid OrganisationId(string name) => kmsa.Organisations.Single(o => o.DisplayName == name).Id;
        Guid VenueId(string name) => kmsa.Venues.Single(v => v.Name == name).Id;

        kmsa.Teams.Where(t => t.OrganisationId == OrganisationId("Ntinda Swifts SC"))
            .Select(t => (t.Name, t.GenderCategory, t.AgeGroup))
            .ShouldBe(
                [("Ntinda Swifts FC", GenderCategory.Men, AgeGroup.Senior), ("Ntinda Swifts FC", GenderCategory.Women, AgeGroup.Senior),
                    ("Ntinda Swifts FC", GenderCategory.Men, AgeGroup.U17)],
                ignoreOrder: true);
        kmsa.Organisations.Single(o => o.DisplayName == "Ntinda Swifts SC").PrimaryVenueId.ShouldBe(VenueId("Bukoto Community Stadium"));
        kmsa.Teams.Single(t => t.Name == "Seeta Sparrows FC" && t.AgeGroup == AgeGroup.U17).HomeVenueId.ShouldBe(VenueId("Seeta Hill Sports Field"));

        // The slice demo gives Bunga Breakers FC its badge and home venue.
        var bunga = kmsa.Teams.Single(t => t.Name == "Bunga Breakers FC" && t.AgeGroup == AgeGroup.Senior);
        (bunga.BadgeAssetId, bunga.HomeVenueId, bunga.NameSince).ShouldBe((null, null, new DateOnly(2020, 1, 1)));

        kmsa.Venues.Where(v => v.SupportedVariants.Count > 0).Select(v => v.Name)
            .ShouldBe(["Bukoto Community Stadium", "Kisaasi Recreation Ground", "Seeta Hill Sports Field"], ignoreOrder: true);
        kmsa.Venues.Single(v => v.Name == "Bukoto Community Stadium").Capacity.ShouldBe(3000);
        kmsa.Venues.ShouldAllBe(v => v.TimeZone == "Africa/Kampala" && v.Latitude == null);
        kmsa.Organisations.ShouldAllBe(o => o.LogoAssetId != null);
        kmsa.Teams.Count(t => t.BadgeAssetId != null).ShouldBe(9);
        kmsa.Teams.ShouldAllBe(t => t.LogoAssetId == null);
    }

    [Fact]
    public async Task Every_change_is_audited_as_the_system_under_its_tenant()
    {
        await SeedDemoAsync(App);

        var entries = await RegistryAuditEntriesAsync();

        entries.ShouldAllBe(e => e.Source == AuditSource.System && e.ActorKind == ActorKind.System && e.ActorId == SystemActor.Id
            && e.Outcome == AuditOutcome.Success && (e.TenantId == _kmsaId || e.TenantId == _elsscId));
        entries.GroupBy(e => e.ActionCode).ToDictionary(g => g.Key, g => g.Count()).ShouldBe(
            new Dictionary<string, int>
            {
                ["venue.create"] = 6,
                ["organisation.create"] = 24,
                ["organisation.logo.set"] = 24,
                ["team.create"] = 33,
                ["team.rename"] = 1,
                ["team.badge.set"] = 9,
                ["team.status.change"] = 1,
            },
            ignoreOrder: true);
        entries.Single(e => e.ActionCode == "team.status.change").Reason.ShouldBe("Folded into the first team");
    }

    [Fact]
    public async Task A_run_completes_what_is_missing_and_finds_a_team_renamed_since()
    {
        await SeedDemoAsync(App);
        await using (var scope = RegistryTestData.ScopeFor(App.Services, _kmsaId, out var context))
        {
            // Test setup: no command leaves an organisation without its logo but removing it.
            var kites = await context.Set<Organisation>().SingleAsync(o => o.DisplayName == "Kansanga Kites", Ct);
            context.Entry(kites).Property(o => o.LogoAssetId).CurrentValue = null;

            // As the slice demo does: Bunga Breakers FC is now Bunga Lakeside FC.
            var bunga = await context.Set<Team>().Include(t => t.Aliases)
                .SingleAsync(t => t.Name == "Bunga Breakers FC" && t.AgeGroup == AgeGroup.Senior, Ct);
            bunga.Rename("Bunga Lakeside FC", "Bunga Lakeside", "BUN", new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 17)).IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync(Ct);
        }

        var before = await RegistryAuditEntriesAsync();
        await using var rerun = WithRerun(App);
        var report = await SeedDemoAsync(rerun);

        report.Seeders.Where(s => s.Key.StartsWith(RerunSeeder<DemoVenueSeeder>.KeyPrefix, StringComparison.Ordinal)).Select(s => s.Outcome)
            .ShouldBe([SeedOutcome.None, new SeedOutcome(Inserted: 0, Updated: 1, Deactivated: 0), SeedOutcome.None]);
        var added = (await RegistryAuditEntriesAsync()).Where(e => before.All(b => b.Id != e.Id)).ShouldHaveSingleItem();
        added.ActionCode.ShouldBe("organisation.logo.set");
        var kmsa = await RegistryAsync(_kmsaId);
        kmsa.Teams.Count.ShouldBe(25);
        kmsa.Teams.Count(t => t.Name == "Bunga Lakeside FC").ShouldBe(1);
    }

    [Fact]
    public async Task A_duplicate_warning_stops_the_run_naming_the_record()
    {
        await SeedDemoAsync(App);
        await using (var scope = RegistryTestData.ScopeFor(App.Services, _kmsaId, out var context))
        {
            // Test setup: a seeded venue renamed by hand, so the next run doesn't find it and the warning does.
            var kisaasi = await context.Set<Venue>().SingleAsync(v => v.Name == "Kisaasi Recreation Ground", Ct);
            context.Entry(kisaasi).Property(v => v.Name).CurrentValue = "Kisaasi Recreation Grounds";
            context.Entry(kisaasi).Property(v => v.NameKey).CurrentValue = Venue.NameKeyOf("Kisaasi Recreation Grounds");
            await context.SaveChangesAsync(Ct);
        }

        await using var rerun = WithRerun(App);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => SeedDemoAsync(rerun));

        failure.Message.ShouldContain("demo venue Kisaasi Recreation Ground");
        failure.Message.ShouldContain("VENUE_DUPLICATE_REVIEW_REQUIRED");
        (await RegistryAsync(_kmsaId)).Venues.Count.ShouldBe(5);
    }

    [Fact]
    public async Task A_suspended_demo_tenant_waits()
    {
        await SeedDemoAsync(App);
        await using (var scope = RegistryTestData.ScopeFor(App.Services, _kmsaId, out var context))
        {
            var kites = await context.Set<Organisation>().SingleAsync(o => o.DisplayName == "Kansanga Kites", Ct);
            context.Entry(kites).Property(o => o.LogoAssetId).CurrentValue = null; // test setup: a step left to do
            await context.SaveChangesAsync(Ct);
        }

        App.Authorization.AllowAll();
        using (var client = App.CreateClient().AsUser(TenantTestData.StaffId))
        {
            using var suspended = await client.PostAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{_kmsaId}/suspend", UriKind.Relative), new { reason = "Season fees outstanding" }, Ct);
            suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using var rerun = WithRerun(App);
        var report = await SeedDemoAsync(rerun);

        report.Seeders.Where(s => s.Key.StartsWith(RerunSeeder<DemoVenueSeeder>.KeyPrefix, StringComparison.Ordinal))
            .Select(s => s.Outcome).ShouldBe([SeedOutcome.None, SeedOutcome.None, SeedOutcome.None]);
        (await RegistryAsync(_kmsaId)).Organisations.Single(o => o.DisplayName == "Kansanga Kites").LogoAssetId.ShouldBeNull();
    }

    [Fact]
    public async Task ELSSC_reads_none_of_KMSAs_registry()
    {
        await SeedDemoAsync(App);

        var elssc = await RegistryAsync(_elsscId);

        (elssc.Venues.Count, elssc.Organisations.Count, elssc.Teams.Count).ShouldBe((1, 4, 8));
        elssc.Venues.ShouldAllBe(v => v.TenantId == _elsscId);
        elssc.Organisations.ShouldAllBe(o => o.TenantId == _elsscId);
        elssc.Teams.ShouldAllBe(t => t.TenantId == _elsscId);
    }

    private static Task<SeedReport> SeedDemoAsync(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

    /// <summary>The app with the three seeders again under new keys and checksums, so the runner runs them.</summary>
    private static WebApplicationFactory<Program> WithRerun(WebApplicationFactory<Program> app) =>
        app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSeeder<RerunSeeder<DemoVenueSeeder>>();
            services.AddSeeder<RerunSeeder<DemoOrganisationSeeder>>();
            services.AddSeeder<RerunSeeder<DemoTeamSeeder>>();
        }));

    /// <summary>The tenant's venues, organisations and teams with what the seeders write, read under that tenant.</summary>
    private async Task<(List<Venue> Venues, List<Organisation> Organisations, List<Team> Teams)> RegistryAsync(Guid tenantId)
    {
        await using var scope = RegistryTestData.ScopeFor(App.Services, tenantId, out var context);
        var venues = await context.Set<Venue>().AsNoTracking().ToListAsync(Ct);
        var organisations = await context.Set<Organisation>().AsNoTracking().ToListAsync(Ct);
        var teams = await context.Set<Team>().AsNoTracking().AsSplitQuery()
            .Include(t => t.Aliases).Include(t => t.StatusHistory).ToListAsync(Ct);
        return (venues, organisations, teams);
    }

    private async Task<List<AuditEntry>> RegistryAuditEntriesAsync()
    {
        // The seed writes more audit entries than one outbox batch: process until nothing is left.
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        for (var batch = 0; batch < 40 && await processor.ProcessOnceAsync(Ct) > 0; batch++)
        {
        }

        string[] prefixes = ["venue.", "organisation.", "team."];
        return [.. (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => prefixes.Any(p => e.ActionCode.StartsWith(p, StringComparison.Ordinal)))];
    }

    /// <summary>A DEMO seeder again, after the real ones and in their order, with a checksum of its own.</summary>
    private sealed class RerunSeeder<TSeeder> : ISeeder
        where TSeeder : ISeeder, new()
    {
        public const string KeyPrefix = "test.demo.rerun.";

        private readonly TSeeder _seeder = new();

        public string Key => KeyPrefix + _seeder.Key;

        public SeedCategory Category => SeedCategory.Demo;

        public int Order => _seeder.Order + 50;

        public string Checksum { get; } = Guid.NewGuid().ToString("N");

        public Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken) => _seeder.RunAsync(context, cancellationToken);
    }
}
