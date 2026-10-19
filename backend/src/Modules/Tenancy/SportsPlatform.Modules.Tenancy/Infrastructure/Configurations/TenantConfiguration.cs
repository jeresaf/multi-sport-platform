using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.tenants</c>: platform-managed and global (no <c>tenant_id</c>, no RLS). The slug is unique across every
/// row, archived ones included, so it is never reused; the database checks its format too.
/// </summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public const string Table = "tenants";
    public const string SlugIndex = "ux_tenants_slug";

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_tenants_status", $"status IN ({StatusValues.TenantStatuses})");
            table.HasCheckConstraint("ck_tenants_slug", $"slug ~ '{TenantSlug.Pattern}'");
            table.HasCheckConstraint("ck_tenants_country_code", "country_code ~ '^[A-Z]{2}$'");
        });

        // "C": byte order, so the list (sorted by slug) pages the same way in every database.
        builder.Property(t => t.Slug).HasMaxLength(TenantSlug.MaxLength).UseCollation("C");
        builder.Property(t => t.LegalName).HasMaxLength(TenantProfile.LegalNameMaxLength);
        builder.Property(t => t.DisplayName).HasMaxLength(TenantProfile.DisplayNameMaxLength);
        builder.Property(t => t.CountryCode).HasMaxLength(2);
        builder.Property(t => t.Status).HasConversion(StatusValues.UpperSnake<TenantStatus>()).HasMaxLength(20);

        builder.Ignore(t => t.Profile);

        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName(SlugIndex);

        builder.HasMany(t => t.StatusHistory).WithOne().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
