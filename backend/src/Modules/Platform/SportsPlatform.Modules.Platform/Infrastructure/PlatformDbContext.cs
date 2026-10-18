using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Modules.Platform.Infrastructure;

/// <summary>The Platform module's unit of work: schema <c>platform</c>, and <c>audit</c> for the audit entries.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, PlatformModule.SchemaName, tenantContext)
{
    protected override Error ConcurrencyConflictError => PlatformErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
}
