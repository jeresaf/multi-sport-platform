using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.tenant_domains</c>: global, no RLS (public requests look hosts up before a tenant is known), so it is an
/// exception in the RLS coverage test. A host belongs to one tenant; a tenant has at most one primary domain.
/// </summary>
internal sealed class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public const string Table = "tenant_domains";
    public const string HostIndex = "ux_tenant_domains_host";

    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_tenant_domains_type", $"type IN ({StatusValues.SqlList<TenantDomainType>()})");
            table.HasCheckConstraint("ck_tenant_domains_status", $"status IN ({StatusValues.SqlList<TenantDomainStatus>()})");
            table.HasCheckConstraint("ck_tenant_domains_host", "host ~ '^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$'");
        });

        builder.Property(d => d.Host).HasMaxLength(TenantHost.MaxLength).UseCollation("C");
        builder.Property(d => d.Type).HasConversion(StatusValues.UpperSnake<TenantDomainType>()).HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion(StatusValues.UpperSnake<TenantDomainStatus>()).HasMaxLength(20);

        builder.HasIndex(d => d.Host).IsUnique().HasDatabaseName(HostIndex);
        builder.HasIndex(d => d.TenantId).IsUnique().HasFilter("is_primary").HasDatabaseName("ux_tenant_domains_primary");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
