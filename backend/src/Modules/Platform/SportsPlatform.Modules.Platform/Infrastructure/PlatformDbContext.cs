using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Platform.Infrastructure;

/// <summary>The Platform module's unit of work (schema <c>platform</c>). Tables arrive with T5–T7.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : ModuleDbContext(options, PlatformModule.SchemaName)
{
    protected override Error ConcurrencyConflictError => PlatformErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
}
