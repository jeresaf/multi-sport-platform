using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.athlete_profiles</c> (S06 D12): tenant-owned (RLS), each pointing at its tenant's person. A person holds
/// at most one open (not archived) profile per variant: a partial unique index. The variant, the positions and the
/// public name are <c>PUBLIC</c> (S06 D3); the dominant side and the status <c>INTERNAL</c>.
/// </summary>
internal sealed class AthleteProfileConfiguration : IEntityTypeConfiguration<AthleteProfile>
{
    public const string Table = "athlete_profiles";
    public const string OpenVariantIndex = "ux_athlete_profiles_tenant_id_person_id_variant_open";

    public void Configure(EntityTypeBuilder<AthleteProfile> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_athlete_profiles_status", $"status IN ({EnumColumns.SqlList<ProfileStatus>()})");
            table.HasCheckConstraint("ck_athlete_profiles_dominant_side", $"dominant_side IN ({EnumColumns.SqlList<DominantSide>()})");
            table.HasCheckConstraint(
                "ck_athlete_profiles_other_positions", $"cardinality(other_positions) <= {AthleteProfile.MaxOtherPositions}");
        });

        builder.Property(a => a.Variant).HasMaxLength(AthleteProfile.VariantMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(a => a.PrimaryPosition).HasMaxLength(AthleteProfile.PositionMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.PrimitiveCollection(a => a.OtherPositions).HasField("_otherPositions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasAnnotation(Pii.Annotation, Pii.Name(PersonalDataClass.Public));
        builder.Property(a => a.DominantSide).HasConversion(EnumColumns.UpperSnake<DominantSide>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.PublicName).HasMaxLength(AthleteProfile.PublicNameMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(a => a.Status).HasConversion(EnumColumns.UpperSnake<ProfileStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);

        builder.HasIndex(a => new { a.TenantId, a.PersonId, a.Variant })
            .IsUnique()
            .HasFilter("status <> 'ARCHIVED'")
            .HasDatabaseName(OpenVariantIndex);

        // The directory's variant filter.
        builder.HasIndex(a => new { a.TenantId, a.Variant });
    }
}

/// <summary>
/// <c>registry.profile_status_changes</c> (S06 D12): append-only (migration <c>AppendOnly</c>), tenant-owned (RLS). One
/// table for every profile kind; it points at the person, and at the profile by kind and ID (no foreign key: a key can't
/// point at one of several tables).
/// </summary>
internal sealed class ProfileStatusChangeConfiguration : IEntityTypeConfiguration<ProfileStatusChange>
{
    public const string Table = "profile_status_changes";

    public void Configure(EntityTypeBuilder<ProfileStatusChange> builder)
    {
        var statuses = EnumColumns.SqlList<ProfileStatus>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_profile_status_changes_profile_kind", $"profile_kind IN ({EnumColumns.SqlList<ProfileKind>()})");
            table.HasCheckConstraint("ck_profile_status_changes_from_status", $"from_status IN ({statuses})");
            table.HasCheckConstraint("ck_profile_status_changes_to_status", $"to_status IN ({statuses})");
            table.HasCheckConstraint("ck_profile_status_changes_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });
        builder.IsAppendOnly();

        builder.Property(c => c.ProfileKind).HasConversion(EnumColumns.UpperSnake<ProfileKind>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.FromStatus).HasConversion(EnumColumns.UpperSnake<ProfileStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ToStatus).HasConversion(EnumColumns.UpperSnake<ProfileStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Reason).HasMaxLength(ProfileStatusChange.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.ProfileKind, c.ProfileId, c.OccurredAt });
        builder.HasIndex(c => new { c.TenantId, c.PersonId });
    }
}
