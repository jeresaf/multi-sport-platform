using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Authorization;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Messaging;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Audit;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Features.Audit.ListAuditEntries;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// <c>audit.audit_entries</c> under RLS (S02 D3): <c>sp_app</c> reads and writes its tenant's entries only, and entries
/// without a tenant not at all; the audit writers and the platform query run on <c>sp_platform</c>. The direct and
/// outbox writes without a tenant (denial, re-drive) are proven by <see cref="AuditTrailTests"/>, which run under RLS.
/// </summary>
public sealed class AuditRlsTests : DatabaseTest
{
    private static readonly Guid _kmsa = new("0192a1b2-0000-7000-8000-00000000c001");
    private static readonly Guid _elssc = new("0192a1b2-0000-7000-8000-00000000c002");

    public AuditRlsTests(PostgresFixture fixture)
        : base(fixture) => App.Consumers.Reset();

    [Fact]
    public async Task The_app_role_reads_its_tenants_entries_only()
    {
        await AppendAsync(_kmsa, _elssc, null);

        (await TenantsVisibleToAppAsync(_kmsa)).ShouldBe([_kmsa]);
        (await TenantsVisibleToAppAsync(_elssc)).ShouldBe([_elssc]);
        (await TenantsVisibleToAppAsync(null)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("0192a1b2-0000-7000-8000-00000000c002")]
    [InlineData(null)]
    public async Task The_app_role_cannot_write_an_entry_of_another_tenant_or_of_none(string? entryTenant)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_kmsa);
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        context.Add(AuditEntry.From(AuditTestData.Record() with { TenantId = entryTenant is null ? null : Guid.Parse(entryTenant) }));

        var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));

        exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_platform_query_reads_every_tenant_and_entries_without_one()
    {
        await AppendAsync(_kmsa, _elssc, null);
        App.Authorization.Allow(PlatformActions.AuditRead);
        using var client = App.CreateClient().AsUser(TenantTestData.StaffId);

        using var response = await client.GetAsync(
            new Uri("/api/platform/v1/audit-entries?action=platform.outbox.redrive", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<ListAuditEntriesResponse>(MessageSerialization.Options, Ct);
        list.ShouldNotBeNull().Items.Select(e => e.TenantId).ShouldBe([_kmsa, _elssc, null], ignoreOrder: true);
    }

    [Fact]
    public async Task An_entry_through_the_outbox_lands_under_its_tenant_with_its_inbox_row()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_kmsa);
            scope.ServiceProvider.GetRequiredService<AuditScope>().Begin(AuthorizationTestActions.FixtureCreate, AuditSource.Api);
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            context.Probes.Add(new MessagingProbe("Kisaasi Recreation Ground"));
            await context.SaveChangesAsync(Ct);
        }

        await App.Services.GetRequiredService<OutboxProcessor>().ProcessOnceAsync(Ct);

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.TenantId.ShouldBe(_kmsa);
        (await TenantsVisibleToAppAsync(_kmsa)).ShouldBe([_kmsa]);
        (await QueryAsync(
                DatabaseRoles.Platform,
                "SELECT count(*) FROM platform.inbox_messages WHERE consumer = @consumer",
                reader => reader.GetInt64(0),
                new NpgsqlParameter("consumer", "platform.audit")))
            .ShouldBe([1L]);
    }

    private async Task AppendAsync(params Guid?[] tenants)
    {
        var log = App.Services.GetRequiredService<IAuditLog>();
        foreach (var tenant in tenants)
        {
            await log.AppendAsync(AuditTestData.Record() with { TenantId = tenant }, Ct);
        }
    }

    /// <summary>The tenants of the entries <c>sp_app</c> sees through Platform's migrated context under a scope tenant.</summary>
    private async Task<List<Guid?>> TenantsVisibleToAppAsync(Guid? tenant)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AuditEntry>()
            .Select(e => e.TenantId).ToListAsync(Ct);
    }
}
