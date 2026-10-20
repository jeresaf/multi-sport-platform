using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Base for a platform service's DbContext (docs/architecture/multi-tenancy.md): registered with
/// <see cref="PersistenceServiceCollectionExtensions.AddPlatformServiceDbContext{TContext}"/> on the <c>Platform</c>
/// connection (<c>sp_platform</c>, BYPASSRLS), never migrated. The architecture tests name the features allowed to use
/// each one.
/// </summary>
public abstract class PlatformServiceDbContext(DbContextOptions options, string schema, ITenantContext tenantContext)
    : ModuleDbContext(options, schema, tenantContext);
