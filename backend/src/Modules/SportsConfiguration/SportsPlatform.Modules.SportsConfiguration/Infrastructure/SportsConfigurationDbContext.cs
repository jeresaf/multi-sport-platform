using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure;

/// <summary>The Sports Configuration module's unit of work: schema <c>config</c>.</summary>
public sealed class SportsConfigurationDbContext(DbContextOptions<SportsConfigurationDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, SportsConfigurationModule.SchemaName, tenantContext)
{
    protected override Error ConcurrencyConflictError => SportsConfigurationErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SportsConfigurationDbContext).Assembly);
}
