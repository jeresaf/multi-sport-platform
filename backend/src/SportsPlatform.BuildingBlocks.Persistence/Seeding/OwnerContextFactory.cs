using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence.Seeding;

/// <summary>
/// Module DbContexts on the <c>Migrator</c> connection, for seeds of read-only global tables and the seed bookkeeping.
/// They stamp audit columns like the runtime contexts, but have no messaging: seeds publish no events. They have no
/// tenant, so they can't write tenant-owned rows.
/// </summary>
public sealed class OwnerContextFactory(
    IEnumerable<ModuleDatabase> databases, IConfiguration configuration, ICurrentActor actor, TimeProvider timeProvider)
{
    public TContext Create<TContext>()
        where TContext : ModuleDbContext
    {
        var database = databases.SingleOrDefault(d => d.ContextType == typeof(TContext))
            ?? throw new InvalidOperationException($"{typeof(TContext).Name} is not a registered module DbContext.");
        var connectionString = PersistenceServiceCollectionExtensions.RequiredConnectionString(
            configuration, ConnectionStringNames.Migrator);

        var noTenant = new TenantContext();
        var options = new DbContextOptionsBuilder<TContext>()
            .UseModuleDefaults<TContext>(connectionString, database.Schema)
            .AddInterceptors(
                new TenantOwnershipInterceptor(noTenant),
                new AuditColumnsInterceptor(actor, timeProvider),
                new TenantConnectionInterceptor(noTenant))
            .Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options, noTenant)!;
    }
}
