using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    public async Task The_query_filter_alone_shows_each_context_only_its_tenant()
    {
        await SaveProbeAsync(_tenantA);
        await SaveProbeAsync(_tenantB);

        // sp_platform bypasses RLS, so only the EF filter separates the tenants; two instances share one cached model.
        await using var underA = PlatformContext(_tenantA);
        await using var underB = PlatformContext(_tenantB);
        await using var withoutTenant = PlatformContext(null);

        (await underA.TenantProbes.Select(p => p.TenantId).ToListAsync(Ct)).ShouldBe([_tenantA]);
        (await underB.TenantProbes.Select(p => p.TenantId).ToListAsync(Ct)).ShouldBe([_tenantB]);
        (await withoutTenant.TenantProbes.CountAsync(Ct)).ShouldBe(0);
        (await underA.TenantProbes.CountAsync(Ct)).ShouldBe(1); // still A after B's query
    }

    [Fact]
    public async Task Saving_a_tenant_owned_entity_without_a_tenant_throws()
    {
        await using var scope = Scope(null, out var context);
        context.TenantProbes.Add(new TenantProbe("Lugogo Indoor Stadium"));

        (await Should.ThrowAsync<TenantIsolationViolationException>(() => context.SaveChangesAsync(Ct)))
            .Violation.ShouldBe(TenantIsolationViolation.NoTenant);
    }

    [Fact]
    public async Task Adding_an_entity_that_carries_another_tenant_throws()
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenantB);
        var context = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var profile = new TenantProfile("Mukono Sports Council", "Mukono Sports", "UG");
        context.Add(Tenant.Create("mukono-sports", profile, SystemActor.Instance, App.Clock.GetUtcNow())); // history carries the new tenant's ID

        (await Should.ThrowAsync<TenantIsolationViolationException>(() => context.SaveChangesAsync(Ct)))
            .Violation.ShouldBe(TenantIsolationViolation.OtherTenant);
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

            (await Should.ThrowAsync<TenantIsolationViolationException>(() => context.SaveChangesAsync(Ct)))
                .Violation.ShouldBe(TenantIsolationViolation.OtherTenant);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_foreign_key_between_tenant_owned_entities_must_pair_the_tenant_ids(bool misaligned)
    {
        var builder = new DbContextOptionsBuilder<KeyCheckContext>();
        builder.UseModuleDefaults<KeyCheckContext>("Host=localhost", "key_check_test");
        using var context = new KeyCheckContext(builder.Options, misaligned);

        // A single-column key, or a composite one that maps branch_id to tenant_id, is refused.
        Should.Throw<InvalidOperationException>(() => context.Model)
            .Message.ShouldContain($"{nameof(Leaf)} → {nameof(Branch)}");
    }

    /// <summary>A context on <c>sp_platform</c> (BYPASSRLS) with no interceptors, under <paramref name="tenantId"/>.</summary>
    private ConventionsTestDbContext PlatformContext(Guid? tenantId)
    {
        var tenant = new TenantContext();
        tenant.Set(tenantId);
        var builder = new DbContextOptionsBuilder<ConventionsTestDbContext>();
        builder.UseModuleDefaults<ConventionsTestDbContext>(
            Fixture.ConnectionString(DatabaseRoles.Platform), ConventionsTestDbContext.SchemaName);
        return new ConventionsTestDbContext(builder.Options, tenant);
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

    /// <summary>A model-check fixture, never registered or migrated (see <c>ModuleDatabaseRegistrationTests</c>).</summary>
    internal sealed class KeyCheckContext(DbContextOptions<KeyCheckContext> options, bool misaligned)
        : ModuleDbContext(options, "key_check_test", new TenantContext())
    {
        public bool Misaligned => misaligned;

        protected override Error ConcurrencyConflictError => ConventionsTestDbContext.ConcurrencyConflict;

        protected override void ConfigureModel(ModelBuilder modelBuilder)
        {
            if (misaligned)
            {
                modelBuilder.Entity<Branch>().HasAlternateKey(b => new { b.TenantId, b.Id });
                modelBuilder.Entity<Leaf>().HasOne<Branch>().WithMany()
                    .HasForeignKey(l => new { l.BranchId, l.TenantId }).HasPrincipalKey(b => new { b.TenantId, b.Id });
            }
            else
            {
                modelBuilder.Entity<Leaf>().HasOne<Branch>().WithMany().HasForeignKey(l => l.BranchId);
            }
        }

        // Each variant is a different model.
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, KeyCheckModelCacheKey>();
    }

    private sealed class KeyCheckModelCacheKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (context.GetType(), ((KeyCheckContext)context).Misaligned, designTime);
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
