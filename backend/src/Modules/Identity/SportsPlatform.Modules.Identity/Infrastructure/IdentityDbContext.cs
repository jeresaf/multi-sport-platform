using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.Modules.Identity.Infrastructure;

/// <summary>
/// The Identity module's unit of work: schema <c>identity</c>. Holds ASP.NET Core Identity's users and their satellite
/// tables (no roles tables: roles are defined in code, S03 D9) and the data protection key ring (D14). Identity's
/// stores don't save by themselves (<see cref="Users.UserStore"/>): handlers save once.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, IdentityModule.SchemaName, tenantContext), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override Error ConcurrencyConflictError => IdentityErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
}
