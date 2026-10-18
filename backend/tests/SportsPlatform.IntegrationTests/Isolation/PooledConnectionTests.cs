using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Persistence;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>A pooled connection never carries a previous scope's tenant: the interceptor writes the value on every open.</summary>
public sealed class PooledConnectionTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _tenantA = new("0192a1b2-0000-7000-8000-0000000000a1");
    private static readonly Guid _tenantB = new("0192a1b2-0000-7000-8000-0000000000b2");

    [Fact]
    public async Task A_reused_connection_gets_the_new_scopes_tenant_or_none()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenantA);
            var context = scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
            context.TenantProbes.Add(new TenantProbe("Kyambogo Sports Ground"));
            await context.SaveChangesAsync(Ct);
        }

        // One connection in the pool, so each context below gets the same server session back; no DISCARD ALL on
        // return (as behind a transaction-pooling proxy), so only the interceptor can clear the previous tenant.
        var connectionString = new NpgsqlConnectionStringBuilder(Fixture.ConnectionString(DatabaseRoles.App))
        {
            MaxPoolSize = 1,
            NoResetOnClose = true,
            ApplicationName = nameof(PooledConnectionTests),
        }.ConnectionString;

        var underA = await SessionAsync(connectionString, _tenantA);
        var withoutTenant = await SessionAsync(connectionString, null);
        var underB = await SessionAsync(connectionString, _tenantB);

        withoutTenant.BackendPid.ShouldBe(underA.BackendPid);
        underB.BackendPid.ShouldBe(underA.BackendPid);
        underA.ShouldSatisfyAllConditions(s => s.Setting.ShouldBe(_tenantA.ToString()), s => s.Rows.ShouldBe(1));
        withoutTenant.ShouldSatisfyAllConditions(s => s.Setting.ShouldBe(string.Empty), s => s.Rows.ShouldBe(0));
        underB.ShouldSatisfyAllConditions(s => s.Setting.ShouldBe(_tenantB.ToString()), s => s.Rows.ShouldBe(0));
    }

    /// <summary>Opens a context with the tenant interceptor and reads the session's tenant setting and visible rows (RLS only).</summary>
    private static async Task<Session> SessionAsync(string connectionString, Guid? tenantId)
    {
        var tenant = new TenantContext();
        tenant.Set(tenantId);
        var builder = new DbContextOptionsBuilder<ConventionsTestDbContext>();
        builder.UseModuleDefaults<ConventionsTestDbContext>(connectionString, ConventionsTestDbContext.SchemaName)
            .AddInterceptors(new TenantConnectionInterceptor(tenant));
        var options = builder.Options;
        await using var context = new ConventionsTestDbContext(options, tenant);
        await context.Database.OpenConnectionAsync(Ct);
        var pid = await context.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(Ct);
        var setting = await context.Database
            .SqlQuery<string>($"SELECT current_setting('app.tenant_id', true) AS \"Value\"").SingleAsync(Ct);
        var rows = await context.Database
            .SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM conventions_test.tenant_probes").SingleAsync(Ct);
        await context.Database.CloseConnectionAsync();
        return new Session(pid, setting, rows);
    }

    private sealed record Session(int BackendPid, string Setting, long Rows);
}
