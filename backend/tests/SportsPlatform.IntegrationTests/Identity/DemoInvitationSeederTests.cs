using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Account.GetMe;
using SportsPlatform.Modules.Identity.Seed.Demo;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Tenancy.Seed.Demo;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// The DEMO seeder's invitations (S03 D18, T4c2b): the pilot customer's people are invited to KMSA and ELSSC with their
/// roles through <c>membership.invite</c> as <c>SYSTEM</c>, emailed by the outbox, never given a password. A pending or
/// accepted invitation isn't sent again; an expired or revoked one is.
/// </summary>
public sealed class DemoInvitationSeederTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _grace = "grace.nakato@kmsa.example";
    private const string _brian = "brian.ssemakula@kmsa.example";
    private const string _agnes = "agnes.namutebi@elssc.example";

    private static readonly Guid _kmsaId = DemoTenantSeeder.TenantId("kmsa");
    private static readonly Guid _elsscId = DemoTenantSeeder.TenantId("elssc");

    private static readonly (Guid Tenant, string Email, string Role)[] _expected =
    [
        (_kmsaId, _grace, IdentityRoles.TenantOwner),
        (_kmsaId, "daniel.kato@kmsa.example", IdentityRoles.TenantAdmin),
        (_kmsaId, _brian, IdentityRoles.Registrar),
        (_kmsaId, "esther.nabirye@kmsa.example", IdentityRoles.ResultsOfficer),
        (_elsscId, _agnes, IdentityRoles.TenantOwner),
    ];

    [Fact]
    public async Task Seeding_DEMO_invites_the_pilot_people_to_their_tenants_with_their_roles_and_no_accounts()
    {
        var report = await SeedDemoAsync(App);

        DemoSeeder(report).ShouldSatisfyAllConditions(
            s => s.Skipped.ShouldBeFalse(),
            s => s.Outcome.ShouldBe(new SeedOutcome(Inserted: 5, Updated: 0, Deactivated: 0)));
        (await InvitationsAsync()).Select(i => (i.Tenant, i.Email, i.Roles, i.Status))
            .ShouldBe([.. _expected.Select(e => (e.Tenant, e.Email, e.Role, "PENDING"))], ignoreOrder: true);
        (await CountAsync("SELECT count(*) FROM identity.users")).ShouldBe(0); // nobody has a password until they accept
        (await CountAsync("SELECT count(*) FROM identity.tenant_memberships")).ShouldBe(0);
    }

    [Fact]
    public async Task Each_invitation_is_audited_as_membership_invite_by_SYSTEM_under_its_tenant_and_emailed()
    {
        await SeedDemoAsync(App);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);

        var invitations = await InvitationsAsync();
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "membership.invite").ToList();
        entries.ShouldAllBe(e => e.Source == AuditSource.System && e.ActorKind == ActorKind.System && e.ActorId == SystemActor.Id
            && e.Outcome == AuditOutcome.Success);
        entries.Select(e => (e.TenantId, e.EntityId)).ShouldBe([.. invitations.Select(i => ((Guid?)i.Tenant, (Guid?)i.Id))], ignoreOrder: true);

        App.Emails.Sent.Select(m => m.To).ShouldBe(_expected.Select(e => e.Email), ignoreOrder: true);
        App.Emails.Sent.ShouldAllBe(m => TokenOf(m).Length > 0);
    }

    [Fact]
    public async Task Whoever_runs_the_demo_accepts_an_invitation_chooses_the_password_and_signs_in()
    {
        await SeedDemoAsync(App);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var password = TestPasswords.New();
        using var anonymous = App.CreateClient();

        using var accepted = await anonymous.AcceptInvitationAsync(TokenOf(EmailTo(_grace)), password, Ct);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync(Ct));
        using var session = App.CreateClient();
        (await session.LoginAsync(_grace, password, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = (await session.GetFromJsonAsync<MeResponse>(AccountTestData.MePath, Json, Ct)).ShouldNotBeNull();
        me.Tenants.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            t => t.TenantId.ShouldBe(_kmsaId),
            t => t.Roles.ShouldBe([IdentityRoles.TenantOwner]));
    }

    [Fact]
    public async Task Seeding_again_sends_nothing_and_an_accepted_invitation_is_not_sent_again()
    {
        await SeedDemoAsync(App);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        using (var anonymous = App.CreateClient())
        {
            using var accepted = await anonymous.AcceptInvitationAsync(TokenOf(EmailTo(_grace)), TestPasswords.New(), Ct);
            accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct); // the acceptance's audit entry
        var invitations = await InvitationsAsync();
        var audited = (await AuditTestData.EntriesAsync(App.Services, Ct)).Count;

        var second = await SeedDemoAsync(App);
        // The checksum is unchanged, so the runner skips the seeder; run it again regardless, as a changed version would.
        await using var rerun = WithRerunSeeder();
        var third = await SeedDemoAsync(rerun);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);

        DemoSeeder(second).Skipped.ShouldBeTrue();
        RerunSeeder(third).ShouldSatisfyAllConditions(
            s => s.Skipped.ShouldBeFalse(),
            s => s.Outcome.ShouldBe(SeedOutcome.None));
        (await InvitationsAsync()).ShouldBe(invitations, ignoreOrder: true);
        App.Emails.Sent.Count.ShouldBe(5);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Count.ShouldBe(audited);
    }

    [Fact]
    public async Task An_expired_invitation_is_replaced_by_a_new_one()
    {
        await SeedDemoAsync(App);
        var first = await InvitationsAsync();
        App.Clock.Advance(TimeSpan.FromDays(8));

        await using var rerun = WithRerunSeeder();
        var report = await SeedDemoAsync(rerun);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);

        RerunSeeder(report).Outcome.ShouldBe(new SeedOutcome(Inserted: 5, Updated: 0, Deactivated: 0));
        var all = await InvitationsAsync();
        all.Where(i => first.Any(f => f.Id == i.Id)).ShouldAllBe(i => i.Status == "REVOKED"); // superseded in the same save
        all.Where(i => first.All(f => f.Id != i.Id)).Select(i => i.Email).ShouldBe(_expected.Select(e => e.Email), ignoreOrder: true);
        App.Emails.Sent.Count.ShouldBe(5); // the expired ones were never emailed: the outbox ran only after the rerun
    }

    [Fact]
    public async Task A_revoked_invitation_is_sent_again()
    {
        await SeedDemoAsync(App);
        var brian = (await InvitationsAsync()).Single(i => i.Email == _brian);
        App.Authorization.AllowAll();
        using (var admin = App.CreateClient().AsUser(Guid.CreateVersion7(), memberOf: [_kmsaId]))
        {
            admin.DefaultRequestHeaders.Add("X-Tenant-Id", _kmsaId.ToString());
            using var revoked = await admin.RevokeInvitationAsync(brian.Id, Ct);
            revoked.StatusCode.ShouldBe(HttpStatusCode.OK, await revoked.Content.ReadAsStringAsync(Ct));
        }

        await using var rerun = WithRerunSeeder();
        var report = await SeedDemoAsync(rerun);

        RerunSeeder(report).Outcome.ShouldBe(new SeedOutcome(Inserted: 1, Updated: 0, Deactivated: 0));
        (await InvitationsAsync()).Where(i => i.Email == _brian).Select(i => i.Status).ShouldBe(["PENDING", "REVOKED"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_suspended_demo_tenant_gets_no_invitations()
    {
        await SeedDemoAsync(App);
        App.Authorization.AllowAll();
        using (var client = App.CreateClient().AsUser(TenantTestData.StaffId))
        {
            using var suspended = await client.PostAsJsonAsync(
                new Uri($"{TenantTestData.TenantsPath}/{_kmsaId}/suspend", UriKind.Relative), new { reason = "Season fees outstanding" }, Ct);
            suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var first = await InvitationsAsync();
        App.Clock.Advance(TimeSpan.FromDays(8)); // every invitation has expired

        await using var rerun = WithRerunSeeder();
        var report = await SeedDemoAsync(rerun);

        RerunSeeder(report).Outcome.ShouldBe(new SeedOutcome(Inserted: 1, Updated: 0, Deactivated: 0));
        (await InvitationsAsync()).Where(i => first.All(f => f.Id != i.Id)).ShouldHaveSingleItem().Email.ShouldBe(_agnes);
    }

    private static SeederResult DemoSeeder(SeedReport report) => report.Seeders.Single(s => s.Key == "identity.demo.invitations");

    private static SeederResult RerunSeeder(SeedReport report) => report.Seeders.Single(s => s.Key == RerunDemoInvitationSeeder.SeederKey);

    private static Task<SeedReport> SeedDemoAsync(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

    /// <summary>The test app with a copy of the seeder whose checksum never matches, so it runs every time.</summary>
    private WebApplicationFactory<Program> WithRerunSeeder() =>
        App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSeeder<RerunDemoInvitationSeeder>()));

    private EmailMessage EmailTo(string email) => App.Emails.Sent.Single(m => m.To == email);

    /// <summary>Every invitation, across tenants; <c>Roles</c> are the role codes joined with commas.</summary>
    private async Task<List<(Guid Id, Guid Tenant, string Email, string Roles, string Status)>> InvitationsAsync() =>
        await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT id, tenant_id, email, array_to_string(role_codes, ','), status FROM identity.invitations",
            r => (r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4)));

    private async Task<long> CountAsync(string sql) =>
        (await QueryAsync(DatabaseRoles.Platform, sql, r => r.GetInt64(0))).Single();

    private sealed class RerunDemoInvitationSeeder : ISeeder
    {
        public const string SeederKey = "test.demo.invitations-rerun";

        private readonly DemoInvitationSeeder _seeder = new();

        public string Key => SeederKey;

        public SeedCategory Category => SeedCategory.Demo;

        public int Order => 11;

        public string Checksum { get; } = Guid.NewGuid().ToString("N");

        public Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken) => _seeder.RunAsync(context, cancellationToken);
    }
}
