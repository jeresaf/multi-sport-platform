using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.official_profiles</c> (S06 D12): tenant-owned (RLS), each pointing at its tenant's person. A person holds
/// at most one open (not archived) official profile: a partial unique index. The status is <c>INTERNAL</c> (S06 D3).
/// </summary>
internal sealed class OfficialProfileConfiguration : IEntityTypeConfiguration<OfficialProfile>
{
    public const string Table = "official_profiles";
    public const string OpenProfileIndex = "ux_official_profiles_tenant_id_person_id_open";

    public void Configure(EntityTypeBuilder<OfficialProfile> builder)
    {
        builder.ToTable(Table, table =>
            table.HasCheckConstraint("ck_official_profiles_status", $"status IN ({EnumColumns.SqlList<ProfileStatus>()})"));

        builder.Property(o => o.Status).HasConversion(EnumColumns.UpperSnake<ProfileStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);

        builder.HasMany(o => o.Capabilities).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.OfficialProfileId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Capabilities).HasField("_capabilities").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.TenantId, o.PersonId })
            .IsUnique()
            .HasFilter("status <> 'ARCHIVED'")
            .HasDatabaseName(OpenProfileIndex);
    }
}

/// <summary>
/// <c>registry.official_capabilities</c> (S06 D12): tenant-owned (RLS), each pointing at its tenant's official profile,
/// one row per variant and role (a unique index). A capability left out of an update is deleted: nothing points at it,
/// and the audit entry names the capabilities added and removed by variant and role. Every column is <c>INTERNAL</c>
/// (S06 D3: grades and licence numbers).
/// </summary>
internal sealed class OfficialCapabilityConfiguration : IEntityTypeConfiguration<OfficialCapability>
{
    public const string Table = "official_capabilities";
    public const string RoleIndex = "ux_official_capabilities_tenant_id_profile_id_variant_role";

    public void Configure(EntityTypeBuilder<OfficialCapability> builder)
    {
        builder.ToTable(Table);

        builder.Property(c => c.Variant).HasMaxLength(OfficialCapability.VariantMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Role).HasMaxLength(OfficialCapability.RoleMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Grade).HasMaxLength(OfficialCapability.GradeMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.LicenceNumber).HasMaxLength(OfficialCapability.LicenceNumberMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.LicenceValidUntil).IsPersonalData(PersonalDataClass.Internal);

        builder.HasIndex(c => new { c.TenantId, c.OfficialProfileId, c.Variant, c.Role }).IsUnique().HasDatabaseName(RoleIndex);

        // The directory's variant filter.
        builder.HasIndex(c => new { c.TenantId, c.Variant });
    }
}
