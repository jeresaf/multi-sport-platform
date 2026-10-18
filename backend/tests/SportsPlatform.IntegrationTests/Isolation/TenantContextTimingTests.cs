using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Persistence;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// S02 D1: the RLS value is written when a connection opens, so the tenant must be set before the first connection
/// that touches tenant data — a read of a global table may come first (tenant creation's slug check), but an explicit
/// transaction keeps its connection open, so the tenant must be set before it begins (#31 R1).
/// </summary>
public sealed class TenantContextTimingTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _tenant = new("0192a1b2-0000-7000-8000-0000000000a1");

    [Fact]
    public async Task A_global_read_before_the_tenant_is_set_leaves_the_next_save_under_the_tenant()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
            (await context.ReferenceProbes.AnyAsync(Ct)).ShouldBeFalse(); // opens and closes a connection without a tenant

            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
            context.TenantProbes.Add(new TenantProbe("Mandela National Stadium annex"));
            await context.SaveChangesAsync(Ct); // a new connection, which carries the tenant (RLS WITH CHECK passes)
        }

        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
            (await scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>().TenantProbes.CountAsync(Ct)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task A_tenant_set_after_a_transaction_began_does_not_reach_its_connection()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync(Ct); // connection opens without a tenant

        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
        context.TenantProbes.Add(new TenantProbe("Wankulukuku Stadium"));

        var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }
}
