using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Modules.Registry.Infrastructure;

/// <summary>The Registry module's unit of work: schema <c>registry</c>.</summary>
public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, RegistryModule.SchemaName, tenantContext)
{
    // A person's code for a row of no aggregate root (none is versioned today).
    protected override Error ConcurrencyConflictError => RegistryErrors.PersonConcurrencyConflict;

    /// <summary>The conflict of the aggregate root whose version failed (S07 D2): <see cref="RegistryConcurrencyConflicts"/>.</summary>
    protected override Error ConcurrencyConflictErrorFor(IReadOnlyList<EntityEntry> entries) =>
        entries.Select(entry => RegistryConcurrencyConflicts.For(entry.Metadata.ClrType)).FirstOrDefault(error => error is not null)
            ?? ConcurrencyConflictError;

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        // Trigram similarity for name search and the duplicate check (S06 D4, ADR-0013). A trusted extension: the
        // database's owner (sp_migrator) creates it. No schema is named (Npgsql's model snapshot drops one), so it goes
        // to the first schema of the migrator's search path, public; a test checks that.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RegistryDbContext).Assembly);
    }
}
