using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.staff_profiles</c> (S06 D12): tenant-owned (RLS), each pointing at its tenant's person. A person holds at
/// most one open (not archived) staff profile: a partial unique index. The status is <c>INTERNAL</c> (S06 D3).
/// </summary>
internal sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public const string Table = "staff_profiles";
    public const string OpenProfileIndex = "ux_staff_profiles_tenant_id_person_id_open";

    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable(Table, table =>
            table.HasCheckConstraint("ck_staff_profiles_status", $"status IN ({EnumColumns.SqlList<ProfileStatus>()})"));

        builder.Property(s => s.Status).HasConversion(EnumColumns.UpperSnake<ProfileStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);

        builder.HasMany(s => s.Capabilities).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.StaffProfileId })
            .HasPrincipalKey(s => new { s.TenantId, s.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Capabilities).HasField("_capabilities").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.TenantId, s.PersonId })
            .IsUnique()
            .HasFilter("status <> 'ARCHIVED'")
            .HasDatabaseName(OpenProfileIndex);
    }
}

/// <summary>
/// <c>registry.staff_capabilities</c> (S06 D12): tenant-owned (RLS), each pointing at its tenant's staff profile, one row
/// per variant and role (a unique index). A capability left out of an update is deleted: nothing points at it, and the
/// audit entry names the capabilities added and removed by variant and role (review #102 R1). Every column is
/// <c>INTERNAL</c> (S06 D3 makes only an athlete's sport public).
/// </summary>
internal sealed class StaffCapabilityConfiguration : IEntityTypeConfiguration<StaffCapability>
{
    public const string Table = "staff_capabilities";
    public const string RoleIndex = "ux_staff_capabilities_tenant_id_staff_profile_id_variant_role";

    public void Configure(EntityTypeBuilder<StaffCapability> builder)
    {
        builder.ToTable(Table);

        builder.Property(c => c.Variant).HasMaxLength(StaffCapability.VariantMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Role).HasMaxLength(StaffCapability.RoleMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Qualification).HasMaxLength(StaffCapability.QualificationMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);

        builder.HasIndex(c => new { c.TenantId, c.StaffProfileId, c.Variant, c.Role }).IsUnique().HasDatabaseName(RoleIndex);

        // The directory's variant filter.
        builder.HasIndex(c => new { c.TenantId, c.Variant });
    }
}
