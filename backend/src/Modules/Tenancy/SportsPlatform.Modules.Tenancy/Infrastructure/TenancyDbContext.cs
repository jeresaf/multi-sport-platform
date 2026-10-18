using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Infrastructure;

/// <summary>The Tenancy module's unit of work: schema <c>tenancy</c>.</summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options)
    : ModuleDbContext(options, TenancyModule.SchemaName)
{
    protected override Error ConcurrencyConflictError => TenancyErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenancyDbContext).Assembly);
}
