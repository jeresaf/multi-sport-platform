using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Tenancy.Infrastructure;

/// <summary>The Tenancy module's unit of work: schema <c>tenancy</c>.</summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, TenancyModule.SchemaName, tenantContext)
{
    protected override Error ConcurrencyConflictError => TenancyErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);

    // A value, not an entity: without this EF would map EntitlementValue as a related table.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<EntitlementValue>()
            .HaveConversion<EntitlementValueConverter>()
            .HaveColumnType(EntitlementValueConverter.ColumnType);
    }
}
