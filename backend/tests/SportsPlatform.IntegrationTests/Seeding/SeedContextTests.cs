using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.Modules.Platform.Domain.Audit;

namespace SportsPlatform.IntegrationTests.Seeding;

/// <summary>
/// <see cref="SeedContext.RunCommandAsync"/> and <see cref="SeedContext.QueryAsTenantAsync"/>: DEMO and TEST seeders run
/// application commands as a tenant, audited with source <c>SYSTEM</c> (S02-T5, #25 R3).
/// </summary>
public sealed class SeedContextTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _kmsaId = DeterministicIds.Create("tenancy.tenant:kmsa");
    private static readonly Guid _elsscId = DeterministicIds.Create("tenancy.tenant:elssc");

    [Fact]
    public async Task A_command_runs_in_its_own_scope_under_the_tenant_as_the_system_actor_and_is_audited_as_SYSTEM()
    {
        var seen = new List<(Guid? Tenant, ActorKind Actor, string? Action, AuditSource Source)>();
        App.Seeds.DemoWork = async (context, ct) =>
        {
            // Two tenants from one seeder: each command gets a scope of its own (D1 allows one tenant per scope).
            foreach (var (tenantId, venue) in new[] { (_kmsaId, "Kisaasi Recreation Ground"), (_elsscId, "Lakeshore High School Pitch") })
            {
                await context.RunCommandAsync(AuthorizationTestActions.FixtureCreate, tenantId, async (services, token) =>
                {
                    var audit = services.GetRequiredService<AuditScope>();
                    seen.Add((services.GetRequiredService<ITenantContext>().TenantId, services.GetRequiredService<ICurrentActor>().Kind,
                        audit.Action?.Code.Value, audit.Source));
                    var db = services.GetRequiredService<MessagingTestDbContext>();
                    db.Probes.Add(new MessagingProbe(venue));
                    await db.SaveChangesAsync(token);
                    return true;
                }, ct);
            }
        };

        await App.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        seen.ShouldBe([
            (_kmsaId, ActorKind.System, "fixture.create", AuditSource.System),
            (_elsscId, ActorKind.System, "fixture.create", AuditSource.System),
        ]);
        var entries = await ProcessedAuditEntriesAsync();
        entries.Where(e => e.ActionCode == "fixture.create").Select(e => (e.TenantId, e.Source, e.ActorKind, e.Outcome)).ShouldBe(
            [(_kmsaId, AuditSource.System, ActorKind.System, AuditOutcome.Success), (_elsscId, AuditSource.System, ActorKind.System, AuditOutcome.Success)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_command_that_saves_nothing_stops_the_run()
    {
        App.Seeds.DemoWork = (context, ct) =>
            context.RunCommandAsync(AuthorizationTestActions.FixtureCreate, _kmsaId, (_, _) => Task.FromResult(true), ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct));

        exception.Message.ShouldContain("saved nothing");
    }

    [Fact]
    public async Task A_command_that_fails_stops_the_run_and_records_nothing_for_its_seeder()
    {
        App.Seeds.DemoWork = (context, ct) => context.RunCommandAsync<bool>(
            AuthorizationTestActions.FixtureCreate, _kmsaId, (_, _) => throw new InvalidOperationException("Fixture venue is closed (test)."), ct);

        await Should.ThrowAsync<InvalidOperationException>(
            () => App.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct));

        (await QueryAsync(DatabaseRoles.App, "SELECT count(*) FROM platform.seed_runs WHERE seeder_key = 'test.demo.probe'", reader => reader.GetInt64(0)))
            .ShouldHaveSingleItem().ShouldBe(0);
        (await ProcessedAuditEntriesAsync()).ShouldNotContain(e => e.ActionCode == "fixture.create");
    }

    [Fact]
    public async Task A_command_without_a_tenant_leaves_the_scope_tenant_unset_for_the_command_to_set()
    {
        App.Seeds.DemoWork = (context, ct) => context.RunCommandAsync(AuthorizationTestActions.FixtureCreate, tenantId: null, async (services, token) =>
        {
            var tenant = services.GetRequiredService<TenantContext>();
            tenant.TenantId.ShouldBeNull();
            tenant.Set(_elsscId); // As tenant creation does, after its global slug check.
            var db = services.GetRequiredService<MessagingTestDbContext>();
            db.Probes.Add(new MessagingProbe("Lakeshore High School Pitch"));
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        await App.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        (await ProcessedAuditEntriesAsync()).ShouldContain(e => e.ActionCode == "fixture.create" && e.TenantId == _elsscId);
    }

    [Fact]
    public async Task A_query_runs_under_the_tenant_and_audits_nothing()
    {
        Guid? seenTenant = null;
        string? seenAction = "not read";
        App.Seeds.DemoWork = (context, ct) => context.QueryAsTenantAsync(_kmsaId, (services, _) =>
        {
            seenTenant = services.GetRequiredService<ITenantContext>().TenantId;
            seenAction = services.GetRequiredService<AuditScope>().Action?.Code.Value;
            return Task.FromResult(true);
        }, ct);

        await App.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Demo), Ct);

        seenTenant.ShouldBe(_kmsaId);
        seenAction.ShouldBeNull();
    }

    [Fact]
    public async Task A_reference_seeder_runs_no_commands()
    {
        App.Seeds.ReferenceWork = (context, ct) =>
            context.RunCommandAsync(AuthorizationTestActions.FixtureCreate, _kmsaId, (_, _) => Task.FromResult(true), ct);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => App.Services.GetRequiredService<SeedRunner>().RunAsync(new SeedRequest(SeedCategory.Reference), Ct));

        exception.Message.ShouldContain("runs no application commands");
    }

    private async Task<List<AuditEntry>> ProcessedAuditEntriesAsync()
    {
        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);
        return await AuditTestData.EntriesAsync(App.Services, Ct);
    }
}
