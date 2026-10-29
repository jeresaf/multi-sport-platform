using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Domain.Entitlements;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.tenant_entitlement_overrides</c> (S04 D5): tenant-owned (RLS). A row changes only when it is removed (the
/// three <c>removed</c> columns are set together). At most one unremoved override per tenant and entitlement, by a
/// partial unique index; replacing one removes the old row and adds the new one in one save. The entitlement code names
/// the catalogue without a foreign key (entitlements are code); the value is <c>jsonb</c> (TenancyDbContext's
/// conventions).
/// </summary>
internal sealed class EntitlementOverrideConfiguration : IEntityTypeConfiguration<EntitlementOverride>
{
    public const string Table = "tenant_entitlement_overrides";

    public void Configure(EntityTypeBuilder<EntitlementOverride> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_tenant_entitlement_overrides_entitlement_code", PlanTables.CodeCheck("entitlement_code"));
            table.HasCheckConstraint(
                "ck_tenant_entitlement_overrides_removed",
                "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) " +
                "OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
        });

        builder.Property(o => o.EntitlementCode).HasMaxLength(EntitlementOverride.EntitlementCodeMaxLength);
        builder.Property(o => o.Reason).HasMaxLength(EntitlementOverride.ReasonMaxLength);
        builder.Property(o => o.RemoveReason).HasMaxLength(EntitlementOverride.ReasonMaxLength);

        builder.HasIndex(o => new { o.TenantId, o.EntitlementCode })
            .IsUnique()
            .HasFilter("removed_at IS NULL")
            .HasDatabaseName("ux_tenant_entitlement_overrides_unremoved");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(o => o.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
