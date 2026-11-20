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
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Registry.Seed.Demo;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Seed.Demo;
using AgeStatus = SportsPlatform.Modules.Registry.Contracts.People.AgeStatus;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The DEMO people (S06 D23, pilot-customer.md): KMSA's about 40 and ELSSC's 10 pupils, created through the Registry's
/// commands as <c>SYSTEM</c>, with the pairs the slice demo needs; idempotent by step.
/// </summary>
public sealed class DemoPeopleSeederTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _kmsaId = DemoTenantSeeder.TenantId("kmsa");
    private static readonly Guid _elsscId = DemoTenantSeeder.TenantId("elssc");

    [Fact]
    public async Task Seeding_DEMO_creates_KMSAs_people_and_ELSSCs_pupils_through_the_commands()
    {
        var report = await SeedDemoAsync(App);

        report.Seeders.Single(s => s.Key == "registry.demo.people").Outcome.ShouldBe(new SeedOutcome(Inserted: 50, Updated: 0, Deactivated: 0));
        var kmsa = await PeopleAsync(_kmsaId);
        var elssc = await PeopleAsync(_elsscId);
        kmsa.Count.ShouldBe(40);
        elssc.Count.ShouldBe(10);
        kmsa.Select(p => p.NameKey).ShouldBeUnique();
        kmsa.Count(p => p.PhotoAssetId != null).ShouldBe(10);
        elssc.Count(p => p.PhotoAssetId != null).ShouldBe(3);
        kmsa.Concat(elssc).ShouldAllBe(p => p.Source == PersonSource.Manual && !p.PublicProfileEnabled);
    }

    [Fact]
    public async Task Minors_are_under_17_on_the_tenants_date_and_some_have_consent()
    {
        await SeedDemoAsync(App);
        var today = TenantCalendar.Today(App.Clock, "Africa/Kampala");

        var kmsa = await PeopleAsync(_kmsaId);
        var kmsaMinors = kmsa.Where(p => Age.StatusOn(p.DateOfBirth, today) == AgeStatus.Minor).ToList();
        kmsaMinors.Count.ShouldBe(10);
        kmsaMinors.Count(p => p.Consents.Count > 0).ShouldBe(4);
        kmsaMinors.ShouldAllBe(p => p.DateOfBirth > today.AddYears(-17));
        var brenda = kmsa.Single(p => p.DisplayName == "Brenda Akello");
        Age.StatusOn(brenda.DateOfBirth, today).ShouldBe(AgeStatus.Minor);
        brenda.Consents.ShouldBeEmpty(); // the demo records her mother's consent
        kmsa.Where(p => !kmsaMinors.Contains(p)).ShouldAllBe(p => p.Consents.Count == 0);

        var elssc = await PeopleAsync(_elsscId);
        elssc.ShouldAllBe(p => Age.StatusOn(p.DateOfBirth, today) == AgeStatus.Minor);
        elssc.Count(p => p.Consents.Count > 0).ShouldBe(7);

        var consents = kmsa.Concat(elssc).SelectMany(p => p.Consents).ToList();
        consents.ShouldAllBe(c => c.Status == ConsentStatus.Active && c.GivenOn <= today && c.Scopes.Contains(ConsentScope.DataProcessing));
        consents.Count(c => c.EvidenceAssetId != null).ShouldBe(6);
    }

    [Fact]
    public async Task Denis_Opoka_holds_three_profiles_and_Ivan_Mukasa_is_archived()
    {
        await SeedDemoAsync(App);

        var kmsa = await PeopleAsync(_kmsaId);
        var denis = kmsa.Single(p => p.DisplayName == "Denis Opoka");
        denis.AthleteProfiles.ShouldHaveSingleItem().PrimaryPosition.ShouldBe("GK");
        denis.StaffProfiles.ShouldHaveSingleItem().Capabilities.ShouldHaveSingleItem().Role.ShouldBe("ASSISTANT_COACH");
        var referee = denis.OfficialProfiles.ShouldHaveSingleItem().Capabilities.ShouldHaveSingleItem();
        (referee.Role, referee.LicenceValidUntil).ShouldBe(("REFEREE", new DateOnly(2027, 6, 30)));

        var ivan = kmsa.Single(p => p.DisplayName == "Ivan Mukasa");
        ivan.Status.ShouldBe(PersonStatus.Archived);
        ivan.StatusHistory.OrderBy(h => h.OccurredAt).Last().Reason.ShouldBe("Left the association");

        // Every player has an athlete profile; staff and officials who don't play have none.
        kmsa.Count(p => p.AthleteProfiles.Count > 0).ShouldBe(36);
        kmsa.Count(p => p.StaffProfiles.Count > 0).ShouldBe(3);
        kmsa.Count(p => p.OfficialProfiles.Count > 0).ShouldBe(3);
        (await PeopleAsync(_elsscId)).ShouldAllBe(p => p.AthleteProfiles.Count == 1);
    }

    [Fact]
    public async Task The_demo_finds_John_Ssekandi_as_likely_and_Mariam_Nankya_as_possible()
    {
        await SeedDemoAsync(App);
        var kmsa = await PeopleAsync(_kmsaId);
        var john = kmsa.Single(p => p.DisplayName == "John Ssekandi");
        var mariam = kmsa.Single(p => p.DisplayName == "Mariam Nankya");

        // Brian types "Ssekandi John" with John's date of birth.
        var reordered = (await CheckAsync(new CheckDuplicatesRequest("Ssekandi", "John", DateOfBirth: john.DateOfBirth))).ShouldHaveSingleItem();
        (reordered.PersonId, reordered.Confidence).ShouldBe((john.Id, DuplicateConfidence.Likely));
        reordered.Evidence.Select(e => e.Code).ShouldContain(DuplicateEvidenceCode.SameDateOfBirth);
        reordered.Evidence.Select(e => e.Code).ShouldContain(DuplicateEvidenceCode.SameNameKey);

        // Shamim, Mariam's twin: her surname and date of birth.
        var twin = (await CheckAsync(new CheckDuplicatesRequest("Shamim", "Nankya", DateOfBirth: mariam.DateOfBirth))).ShouldHaveSingleItem();
        (twin.PersonId, twin.Confidence).ShouldBe((mariam.Id, DuplicateConfidence.Possible));
    }

    [Fact]
    public async Task The_seeded_near_duplicates_are_decided_with_their_reasons_and_not_warned_again()
    {
        await SeedDemoAsync(App);
        var kmsa = await PeopleAsync(_kmsaId);

        var decisions = await DecisionsAsync(_kmsaId);
        decisions.Count.ShouldBe(2);
        decisions.ShouldAllBe(d => d.Decision == DuplicateDecisionKind.NotSame);
        decisions.Select(d => d.Reason).ShouldBe(
            ["Not the same person: another player of the same name", "Twin sister, different first name"], ignoreOrder: true);

        foreach (var name in new[] { "Peter Okot", "Nakato Namaganda" })
        {
            var person = kmsa.Single(p => p.DisplayName == name);
            var check = new CheckDuplicatesRequest(
                person.FirstName, person.LastName, person.MiddleName, DateOfBirth: person.DateOfBirth, Nationality: person.Nationality,
                PersonId: person.Id);
            (await CheckAsync(check)).ShouldBeEmpty(name);
        }

        (await DecisionsAsync(_elsscId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_change_is_audited_as_the_system_under_its_tenant()
    {
        await SeedDemoAsync(App);

        var entries = await RegistryAuditEntriesAsync();

        entries.ShouldAllBe(e => e.Source == AuditSource.System && e.ActorKind == ActorKind.System && e.ActorId == SystemActor.Id
            && e.Outcome == AuditOutcome.Success && e.EntityType == "Person");
        var creates = entries.Where(e => e.ActionCode == "person.create").ToList();
        creates.Count.ShouldBe(50);
        creates.Count(e => e.TenantId == _elsscId).ShouldBe(10);
        creates.Where(e => e.Reason != null).Select(e => e.Reason).ShouldBe(
            ["Not the same person: another player of the same name", "Twin sister, different first name"], ignoreOrder: true);
        entries.Count(e => e.ActionCode == "person.photo.set").ShouldBe(13);
        entries.Count(e => e.ActionCode == "person.consent.record").ShouldBe(11);
        entries.Count(e => e.ActionCode == "person.consent_evidence.upload").ShouldBe(6);
        entries.Single(e => e.ActionCode == "person.archive").Reason.ShouldBe("Left the association");
    }

    [Fact]
    public async Task A_run_on_seeded_data_changes_nothing_and_completes_what_is_missing()
    {
        await SeedDemoAsync(App);
        var before = (await RegistryAuditEntriesAsync()).Count;

        await using var rerun = App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunSeeder>()));
        var unchanged = await SeedDemoAsync(rerun);

        unchanged.Seeders.Single(s => s.Key == RerunSeeder.SeederKey).Outcome.ShouldBe(SeedOutcome.None);
        (await RegistryAuditEntriesAsync()).Count.ShouldBe(before);

        // Someone brought Ivan back: the next run archives him again, and only that.
        var ivan = (await PeopleAsync(_kmsaId)).Single(p => p.DisplayName == "Ivan Mukasa");
        await RegistryTestData.ChangeStatusAsync(App.Services, _kmsaId, ivan.Id, PersonTransition.Unarchive, Ct);
        await using var again = App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunSeeder>()));
        var completed = await SeedDemoAsync(again);

        completed.Seeders.Single(s => s.Key == RerunSeeder.SeederKey).Outcome.ShouldBe(new SeedOutcome(Inserted: 0, Updated: 1, Deactivated: 0));
        (await PeopleAsync(_kmsaId)).Single(p => p.Id == ivan.Id).Status.ShouldBe(PersonStatus.Archived);
        (await RegistryAuditEntriesAsync()).Count.ShouldBe(before + 1);
    }

    [Fact]
    public async Task A_suspended_demo_tenant_waits()
    {
        await SeedDemoAsync(App);
        var ivan = (await PeopleAsync(_kmsaId)).Single(p => p.DisplayName == "Ivan Mukasa");
        await RegistryTestData.ChangeStatusAsync(App.Services, _kmsaId, ivan.Id, PersonTransition.Unarchive, Ct);
        App.Authorization.AllowAll();
        using (var client = App.CreateClient().AsUser(TenantTestData.StaffId))
        {
            using var suspended = await client.PostAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{_kmsaId}/suspend", UriKind.Relative), new { reason = "Season fees outstanding" }, Ct);
            suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using var rerun = App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunSeeder>()));
        var report = await SeedDemoAsync(rerun);

        report.Seeders.Single(s => s.Key == RerunSeeder.SeederKey).Outcome.ShouldBe(SeedOutcome.None);
        (await PeopleAsync(_kmsaId)).Single(p => p.Id == ivan.Id).Status.ShouldBe(PersonStatus.Active);
    }

    [Fact]
    public async Task ELSSC_reads_none_of_KMSAs_people()
    {
        await SeedDemoAsync(App);
        var kmsaIds = (await PeopleAsync(_kmsaId)).Select(p => p.Id).ToHashSet();

        (await PeopleAsync(_elsscId)).ShouldAllBe(p => p.TenantId == _elsscId && !kmsaIds.Contains(p.Id));
    }

    private static Task<SeedReport> SeedDemoAsync(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

    /// <summary>The tenant's people with everything the seeder writes, read under that tenant.</summary>
    private async Task<List<Person>> PeopleAsync(Guid tenantId)
    {
        await using var scope = RegistryTestData.ScopeFor(App.Services, tenantId, out var context);
        return await context.Set<Person>().AsNoTracking().AsSplitQuery()
            .Include(p => p.AthleteProfiles)
            .Include(p => p.StaffProfiles).ThenInclude(s => s.Capabilities)
            .Include(p => p.OfficialProfiles).ThenInclude(o => o.Capabilities)
            .Include(p => p.Consents)
            .Include(p => p.StatusHistory)
            .ToListAsync(Ct);
    }

    private async Task<List<DuplicateDecision>> DecisionsAsync(Guid tenantId)
    {
        await using var scope = RegistryTestData.ScopeFor(App.Services, tenantId, out var context);
        return await context.Set<DuplicateDecision>().AsNoTracking().ToListAsync(Ct);
    }

    private async Task<IReadOnlyList<DuplicateCandidateResponse>> CheckAsync(CheckDuplicatesRequest request)
    {
        await using var scope = RegistryTestData.ScopeFor(App.Services, _kmsaId, out _);
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(SystemActor.Instance);
        var (answer, refused) = await scope.ServiceProvider.GetRequiredService<CheckDuplicatesHandler>().HandleAsync(request, Ct);
        return answer.ShouldNotBeNull(refused?.Error.Code).Candidates;
    }

    private async Task<List<AuditEntry>> RegistryAuditEntriesAsync()
    {
        // The seed writes more audit entries than one outbox batch: process until nothing is left.
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        for (var batch = 0; batch < 20 && await processor.ProcessOnceAsync(Ct) > 0; batch++)
        {
        }

        string[] prefixes = ["person.", "athlete_profile.", "staff_profile.", "official_profile."];
        return [.. (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => prefixes.Any(p => e.ActionCode.StartsWith(p, StringComparison.Ordinal)))];
    }

    private sealed class RerunSeeder : ISeeder
    {
        public const string SeederKey = "test.demo.people-rerun";

        private readonly DemoPeopleSeeder _seeder = new();

        public string Key => SeederKey;

        public SeedCategory Category => SeedCategory.Demo;

        public int Order => 21;

        public string Checksum { get; } = Guid.NewGuid().ToString("N");

        public Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken) => _seeder.RunAsync(context, cancellationToken);
    }
}
