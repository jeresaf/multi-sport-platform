using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Platform.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Platform.Infrastructure;

/// <summary>
/// The audit trail as a platform service, on the <c>Platform</c> connection (<c>sp_platform</c>, BYPASSRLS; S02 D3):
/// it writes entries of every tenant and of none, and the platform audit query reads across tenants. Maps only
/// <c>audit.audit_entries</c> (and, from the base, Platform's outbox and inbox, so a consumer's inbox row commits with
/// its entry). Never migrated: <see cref="PlatformDbContext"/> owns the tables. Used only by <c>Features.Audit</c>.
/// </summary>
public sealed class PlatformAuditDbContext(DbContextOptions<PlatformAuditDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, PlatformModule.SchemaName, tenantContext)
{
    protected override Error ConcurrencyConflictError => PlatformErrors.ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
}
