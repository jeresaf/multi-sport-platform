using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.persons</c> (S06 D2–D4): tenant-owned (RLS). Every personal column is classified. The name key has a
/// trigram index for similarity; the directory sorts by the normalised last and first name. The photo names a Platform
/// media asset without a foreign key (another module's schema).
/// </summary>
internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public const string Table = "persons";
    public const string NameKeyIndex = "ix_persons_name_key_trgm";
    public const string DirectoryIndex = "ix_persons_tenant_id_last_name_key_first_name_key_id";

    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_persons_status", $"status IN ({EnumColumns.SqlList<PersonStatus>()})");
            table.HasCheckConstraint("ck_persons_gender", $"gender IN ({EnumColumns.SqlList<Gender>()})");
            table.HasCheckConstraint("ck_persons_source", $"source IN ({EnumColumns.SqlList<PersonSource>()})");
            table.HasCheckConstraint("ck_persons_nationality", "nationality ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint("ck_persons_secondary_nationality", "secondary_nationality ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint(
                "ck_persons_height_cm", $"height_cm BETWEEN {PersonBiodata.MinHeightCm} AND {PersonBiodata.MaxHeightCm}");
            table.HasCheckConstraint(
                "ck_persons_weight_kg", $"weight_kg BETWEEN {PersonBiodata.MinWeightKg:0} AND {PersonBiodata.MaxWeightKg:0}");
            table.HasCheckConstraint("ck_persons_name_keys", "first_name_key <> '' AND last_name_key <> '' AND name_key <> ''");
        });

        builder.Property(p => p.FirstName).HasMaxLength(PersonBiodata.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.MiddleName).HasMaxLength(PersonBiodata.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.LastName).HasMaxLength(PersonBiodata.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.OtherNames).HasMaxLength(PersonBiodata.OtherNamesMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.DisplayName).HasMaxLength(PersonBiodata.DisplayNameMaxLength).IsPersonalData(PersonalDataClass.Public);

        // "C": byte order, so the directory (sorted by these keys) pages the same way in every database.
        builder.Property(p => p.FirstNameKey).HasMaxLength(PersonBiodata.NameMaxLength).UseCollation("C")
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.LastNameKey).HasMaxLength(PersonBiodata.NameMaxLength).UseCollation("C")
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.NameKey).HasMaxLength(NameKeyMaxLength).IsPersonalData(PersonalDataClass.Internal);

        builder.Property(p => p.Gender).HasConversion(EnumColumns.UpperSnake<Gender>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.DateOfBirth).IsPersonalData(PersonalDataClass.Private);
        builder.Property(p => p.Nationality).HasMaxLength(2).IsPersonalData(PersonalDataClass.Public);
        builder.Property(p => p.SecondaryNationality).HasMaxLength(2).IsPersonalData(PersonalDataClass.Public);
        builder.Property(p => p.HeightCm).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.WeightKg).HasPrecision(4, 1).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.PhotoAssetId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(p => p.PublicProfileEnabled).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.Status).HasConversion(EnumColumns.UpperSnake<PersonStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(p => p.Source).HasConversion(EnumColumns.UpperSnake<PersonSource>()).HasMaxLength(20);

        builder.HasAlternateKey(p => new { p.TenantId, p.Id }).HasName("ak_persons_tenant_id_id");
        builder.HasMany(p => p.Aliases).WithOne()
            .HasForeignKey(a => new { a.TenantId, a.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Aliases).HasField("_aliases").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.StatusHistory).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.AthleteProfiles).WithOne()
            .HasForeignKey(a => new { a.TenantId, a.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.AthleteProfiles).HasField("_athleteProfiles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.StaffProfiles).WithOne()
            .HasForeignKey(s => new { s.TenantId, s.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.StaffProfiles).HasField("_staffProfiles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.OfficialProfiles).WithOne()
            .HasForeignKey(o => new { o.TenantId, o.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.OfficialProfiles).HasField("_officialProfiles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.ProfileStatusHistory).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.ProfileStatusHistory).HasField("_profileStatusHistory")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(p => p.Consents).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.PersonId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Consents).HasField("_consents").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.NameKey).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName(NameKeyIndex);
        builder.HasIndex(p => new { p.TenantId, p.LastNameKey, p.FirstNameKey, p.Id }).HasDatabaseName(DirectoryIndex);
    }

    // Four parts and the spaces between them.
    public const int NameKeyMaxLength = (3 * PersonBiodata.NameMaxLength) + PersonBiodata.OtherNamesMaxLength + 3;
}

/// <summary>
/// <c>registry.person_aliases</c> (S06 D10): tenant-owned (RLS), each pointing at its tenant's person. A removed alias
/// keeps its row (the three <c>remove</c> columns are set together).
/// </summary>
internal sealed class PersonAliasConfiguration : IEntityTypeConfiguration<PersonAlias>
{
    public const string Table = "person_aliases";
    public const string NameKeyIndex = "ix_person_aliases_name_key_trgm";

    public void Configure(EntityTypeBuilder<PersonAlias> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_person_aliases_kind", $"kind IN ({EnumColumns.SqlList<AliasKind>()})");
            table.HasCheckConstraint("ck_person_aliases_name_key", "name_key <> ''");
            table.HasCheckConstraint(
                "ck_person_aliases_removed",
                "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) " +
                "OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
        });

        builder.Property(a => a.Name).HasMaxLength(PersonAlias.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.NameKey).HasMaxLength(PersonAlias.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.Kind).HasConversion(EnumColumns.UpperSnake<AliasKind>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.RemoveReason).HasMaxLength(PersonAlias.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Ignore(a => a.IsRemoved);

        builder.HasIndex(a => a.NameKey).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName(NameKeyIndex);
        builder.HasIndex(a => new { a.TenantId, a.PersonId });
    }
}

/// <summary><c>registry.person_status_changes</c> (S06 D9): append-only (migration <c>AppendOnly</c>), tenant-owned (RLS).</summary>
internal sealed class PersonStatusChangeConfiguration : IEntityTypeConfiguration<PersonStatusChange>
{
    public const string Table = "person_status_changes";

    public void Configure(EntityTypeBuilder<PersonStatusChange> builder)
    {
        var statuses = EnumColumns.SqlList<PersonStatus>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_person_status_changes_from_status", $"from_status IN ({statuses})");
            table.HasCheckConstraint("ck_person_status_changes_to_status", $"to_status IN ({statuses})");
            table.HasCheckConstraint("ck_person_status_changes_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });
        builder.IsAppendOnly();

        // A person's states (DECEASED among them) and why they changed are INTERNAL, like the person's status (S06 D3).
        builder.Property(c => c.FromStatus).HasConversion(EnumColumns.UpperSnake<PersonStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ToStatus).HasConversion(EnumColumns.UpperSnake<PersonStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Reason).HasMaxLength(PersonStatusChange.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.PersonId, c.OccurredAt });
    }
}

/// <summary>
/// <c>registry.external_identifiers</c> (S06 D10; data-conventions.md, Identifiers): tenant-owned (RLS). A value is
/// unique per tenant, authority and entity type for ever. No foreign key to the entity: one table serves every registry
/// entity type (people in S06, venues from S07-T1c).
/// </summary>
internal sealed class ExternalIdentifierConfiguration : IEntityTypeConfiguration<ExternalIdentifier>
{
    public const string Table = "external_identifiers";
    public const string ValueIndex = "ux_external_identifiers_tenant_id_authority_entity_type_value";

    public void Configure(EntityTypeBuilder<ExternalIdentifier> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_external_identifiers_entity_type", $"entity_type IN ({EnumColumns.SqlList<ExternalIdentifierEntityType>()})");
            table.HasCheckConstraint("ck_external_identifiers_authority", $"authority ~ '{ExternalIdentifier.AuthorityPattern}'");
            table.HasCheckConstraint("ck_external_identifiers_value", "value <> '' AND value = btrim(value)");
            table.HasCheckConstraint(
                "ck_external_identifiers_validity", "valid_from IS NULL OR valid_to IS NULL OR valid_to >= valid_from");
        });

        builder.Property(i => i.EntityType).HasConversion(EnumColumns.UpperSnake<ExternalIdentifierEntityType>()).HasMaxLength(20);
        builder.Property(i => i.Authority).HasMaxLength(ExternalIdentifier.AuthorityMaxLength);
        builder.Property(i => i.Value).HasMaxLength(ExternalIdentifier.ValueMaxLength).IsPersonalData(PersonalDataClass.Internal);

        builder.HasIndex(i => new { i.TenantId, i.Authority, i.EntityType, i.Value }).IsUnique().HasDatabaseName(ValueIndex);
        builder.HasIndex(i => new { i.TenantId, i.EntityType, i.EntityId });
    }
}
