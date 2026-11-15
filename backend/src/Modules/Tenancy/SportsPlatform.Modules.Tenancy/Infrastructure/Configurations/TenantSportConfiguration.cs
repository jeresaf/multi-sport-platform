using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Domain.Sports;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.tenant_sports</c> (S05 D12): tenant-owned (RLS), one row per enablement. A row changes only when it is
/// closed (the three <c>disabled</c> columns are set together). At most one open row per tenant and variant, by a partial
/// unique index. The variant names Sports Configuration's definitions without a foreign key (another module's schema).
/// </summary>
internal sealed class TenantSportConfiguration : IEntityTypeConfiguration<TenantSport>
{
    public const string Table = "tenant_sports";
    public const string OpenVariantIndex = "ux_tenant_sports_open";

    public void Configure(EntityTypeBuilder<TenantSport> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_tenant_sports_variant", PlanTables.CodeCheck("variant"));
            table.HasCheckConstraint(
                "ck_tenant_sports_disabled",
                "(disabled_at IS NULL AND disabled_by IS NULL AND disable_reason IS NULL) " +
                "OR (disabled_at IS NOT NULL AND disabled_by IS NOT NULL AND disable_reason IS NOT NULL)");
        });

        builder.Property(s => s.Variant).HasMaxLength(TenantSport.VariantMaxLength);
        builder.Property(s => s.DisableReason).HasMaxLength(TenantSport.ReasonMaxLength);

        builder.HasIndex(s => new { s.TenantId, s.Variant })
            .IsUnique()
            .HasFilter("disabled_at IS NULL")
            .HasDatabaseName(OpenVariantIndex);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
