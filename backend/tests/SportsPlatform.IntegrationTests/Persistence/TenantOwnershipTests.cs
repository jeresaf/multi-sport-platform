using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

/// <summary>The application layer of tenant isolation: the query filter, stamping and the tenant checks before a save.</summary>
public sealed class TenantOwnershipTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _tenantA = new("0192a1b2-0000-7000-8000-0000000000a1");
    private static readonly Guid _tenantB = new("0192a1b2-0000-7000-8000-0000000000b2");

    [Fact]
    public async Task A_new_entity_gets_the_scopes_tenant()
    {
        var probeId = await SaveProbeAsync(_tenantA);

        await using var scope = Scope(_tenantA, out var context);
        (await context.TenantProbes.SingleAsync(Ct)).ShouldSatisfyAllConditions(
            probe => probe.Id.ShouldBe(probeId),
            probe => probe.TenantId.ShouldBe(_tenantA));
    }

    [Fact]
    public async Task Queries_see_only_the_scopes_tenant_and_nothing_without_one()
    {
        await SaveProbeAsync(_tenantA);
        await SaveProbeAsync(_tenantB);

        await using (var scope = Scope(_tenantA, out var context))
        {
            (await context.TenantProbes.Select(p => p.TenantId).ToListAsync(Ct)).ShouldBe([_tenantA]);
        }

        await using (var scope = Scope(null, out var context))
        {
            (await context.TenantProbes.CountAsync(Ct)).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Saving_a_tenant_owned_entity_without_a_tenant_throws()
    {
        await using var scope = Scope(null, out var context);
        context.TenantProbes.Add(new TenantProbe("Lugogo Indoor Stadium"));

        (await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct)))
            .Message.ShouldContain("no tenant");
    }

    [Fact]
    public async Task Adding_an_entity_that_carries_another_tenant_throws()
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenantB);
        var context = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var profile = new TenantProfile("Mukono Sports Council", "Mukono Sports", "UG", "Africa/Kampala", "en", "UGX", PublicEnabled: false);
        context.Add(Tenant.Create("mukono-sports", profile, SystemActor.Instance, App.Clock.GetUtcNow())); // history carries the new tenant's ID

        (await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct)))
            .Message.ShouldContain("another tenant");
    }

    [Fact]
    public async Task Moving_an_entity_to_another_tenant_throws()
    {
        var probeId = await SaveProbeAsync(_tenantA);
        await using (var scope = Scope(_tenantA, out var context))
        {
            context.TenantProbeNotes.Add(new TenantProbeNote(probeId, "Goal nets replaced"));
            await context.SaveChangesAsync(Ct);
        }

        // The probe's TenantId is in its alternate key, which EF itself never changes; the note's is only in a foreign key.
        await using (var scope = Scope(_tenantA, out var context))
        {
            var note = await context.TenantProbeNotes.SingleAsync(Ct);
            context.Entry(note).Property(n => n.TenantId).CurrentValue = _tenantB;

            (await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct)))
                .Message.ShouldContain("another tenant");
        }
    }

    [Fact]
    public void A_single_column_foreign_key_between_tenant_owned_entities_is_refused()
    {
        var builder = new DbContextOptionsBuilder<SingleColumnKeyContext>();
        builder.UseModuleDefaults<SingleColumnKeyContext>("Host=localhost", "single_key_test");
        using var context = new SingleColumnKeyContext(builder.Options);

        Should.Throw<InvalidOperationException>(() => context.Model)
            .Message.ShouldContain($"{nameof(Leaf)} → {nameof(Branch)}");
    }

    private async Task<Guid> SaveProbeAsync(Guid tenantId)
    {
        await using var scope = Scope(tenantId, out var context);
        var probe = new TenantProbe("Kampala Pitch Two");
        context.TenantProbes.Add(probe);
        await context.SaveChangesAsync(Ct);
        return probe.Id;
    }

    private AsyncServiceScope Scope(Guid? tenantId, out ConventionsTestDbContext context)
    {
        var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        context = scope.ServiceProvider.GetRequiredService<ConventionsTestDbContext>();
        return scope;
    }

    private sealed class SingleColumnKeyContext(DbContextOptions<SingleColumnKeyContext> options)
        : ModuleDbContext(options, "single_key_test", new TenantContext())
    {
        protected override Error ConcurrencyConflictError => ConventionsTestDbContext.ConcurrencyConflict;

        protected override void ConfigureModel(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Leaf>().HasOne<Branch>().WithMany().HasForeignKey(l => l.BranchId);
    }

    private sealed class Branch : Entity, ITenantOwned
    {
        private Branch()
        {
        }

        public Guid TenantId { get; private set; }
    }

    private sealed class Leaf : Entity, ITenantOwned
    {
        private Leaf()
        {
        }

        public Guid TenantId { get; private set; }

        public Guid BranchId { get; private set; }
    }
}
