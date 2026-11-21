using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.venues</c> (S07 D3): tenant-owned (RLS). A venue holds no person's data: what the public reads may show
/// is <c>PUBLIC</c>, the keys and the source <c>INTERNAL</c> (D16). The slug is unique per tenant among every status
/// (D9); the name key has a trigram index for the duplicate check (D13). Supported variants are sport codes, checked
/// against Sports Configuration's definitions by the use case (no foreign key: another module's schema).
/// </summary>
internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public const string Table = "venues";
    public const string SlugIndex = "ux_venues_tenant_id_slug";
    public const string NameKeyIndex = "ix_venues_name_key_trgm";
    public const string DirectoryIndex = "ix_venues_tenant_id_sort_key_id";

    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        var surfaces = EnumColumns.SqlList<VenueSurface>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_venues_status", $"status IN ({EnumColumns.SqlList<VenueStatus>()})");
            table.HasCheckConstraint("ck_venues_type", $"type IN ({EnumColumns.SqlList<VenueType>()})");
            table.HasCheckConstraint("ck_venues_source", $"source IN ({EnumColumns.SqlList<VenueSource>()})");
            table.HasCheckConstraint("ck_venues_country", "country ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint(
                "ck_venues_slug",
                $"slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN {Slugs.MinLength} AND {Slugs.MaxLength}");
            table.HasCheckConstraint("ck_venues_name_key", "name_key <> ''");
            table.HasCheckConstraint("ck_venues_sort_key", "sort_key <> ''");
            table.HasCheckConstraint(
                "ck_venues_coordinates",
                "(latitude IS NULL AND longitude IS NULL) OR " +
                $"(latitude BETWEEN -{GeoPoint.MaxLatitude:0} AND {GeoPoint.MaxLatitude:0} " +
                $"AND longitude BETWEEN -{GeoPoint.MaxLongitude:0} AND {GeoPoint.MaxLongitude:0})");
            table.HasCheckConstraint("ck_venues_capacity", $"capacity BETWEEN 0 AND {VenueDetails.MaxCapacity}");
            table.HasCheckConstraint(
                "ck_venues_surfaces",
                $"cardinality(surfaces) <= {VenueDetails.MaxSurfaces} AND surfaces <@ ARRAY[{surfaces}]::character varying(20)[]");
            table.HasCheckConstraint(
                "ck_venues_supported_variants", $"cardinality(supported_variants) <= {VenueDetails.MaxSupportedVariants}");
        });

        builder.Property(v => v.Name).HasMaxLength(VenueDetails.NameMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.ShortName).HasMaxLength(VenueDetails.ShortNameMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.NameKey).HasMaxLength(VenueDetails.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);

        // "C": byte order, so the directory sorts and pages the same way in every database, whatever its locale.
        builder.Property(v => v.SortKey).HasMaxLength(VenueDetails.NameMaxLength).UseCollation("C")
            .IsPersonalData(PersonalDataClass.Internal);

        // "C": byte order, as the sort key.
        builder.Property(v => v.Slug).HasMaxLength(Slugs.MaxLength).UseCollation("C").IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.Type).HasConversion(EnumColumns.UpperSnake<VenueType>()).HasMaxLength(30)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.AddressLine).HasMaxLength(VenueDetails.AddressLineMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.City).HasMaxLength(VenueDetails.CityMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.CityKey).HasMaxLength(VenueDetails.CityMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(v => v.Region).HasMaxLength(VenueDetails.RegionMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.Country).HasMaxLength(2).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.Latitude).HasPrecision(9, 6).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.Longitude).HasPrecision(9, 6).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.TimeZone).HasMaxLength(VenueDetails.TimeZoneMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.Capacity).IsPersonalData(PersonalDataClass.Public);
        builder.PrimitiveCollection(v => v.Surfaces).HasField("_surfaces")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasAnnotation(Pii.Annotation, Pii.Name(PersonalDataClass.Public))
            .ElementType(element => element.HasConversion(EnumColumns.UpperSnake<VenueSurface>()).HasMaxLength(20));
        builder.PrimitiveCollection(v => v.SupportedVariants).HasField("_supportedVariants")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasAnnotation(Pii.Annotation, Pii.Name(PersonalDataClass.Public))
            .ElementType(element => element.HasMaxLength(VenueDetails.VariantMaxLength));
        builder.Property(v => v.Status).HasConversion(EnumColumns.UpperSnake<VenueStatus>()).HasMaxLength(30)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(v => v.Source).HasConversion(EnumColumns.UpperSnake<VenueSource>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Ignore(v => v.Location);

        builder.HasAlternateKey(v => new { v.TenantId, v.Id }).HasName("ak_venues_tenant_id_id");
        builder.HasMany(v => v.StatusHistory).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.VenueId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(v => v.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(v => v.Aliases).WithOne()
            .HasForeignKey(a => new { a.TenantId, a.VenueId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(v => v.Aliases).HasField("_aliases").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(v => new { v.TenantId, v.Slug }).IsUnique().HasDatabaseName(SlugIndex);
        builder.HasIndex(v => new { v.TenantId, v.SortKey, v.Id }).HasDatabaseName(DirectoryIndex);
        builder.HasIndex(v => v.NameKey).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName(NameKeyIndex);
        builder.HasIndex(v => new { v.TenantId, v.Latitude, v.Longitude });
    }
}

/// <summary>
/// <c>registry.venue_aliases</c> (S07 D10): tenant-owned (RLS), each pointing at its tenant's venue. A removed alias keeps
/// its row (the three <c>remove</c> columns are set together). Aliases other than former names are <c>INTERNAL</c> (D16).
/// </summary>
internal sealed class VenueAliasConfiguration : IEntityTypeConfiguration<VenueAlias>
{
    public const string Table = "venue_aliases";
    public const string NameKeyIndex = "ix_venue_aliases_name_key_trgm";

    public void Configure(EntityTypeBuilder<VenueAlias> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_venue_aliases_kind", $"kind IN ({EnumColumns.SqlList<AliasKind>()})");
            table.HasCheckConstraint("ck_venue_aliases_name_key", "name_key <> ''");
            table.HasCheckConstraint(
                "ck_venue_aliases_removed",
                "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) " +
                "OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
        });

        builder.Property(a => a.Name).HasMaxLength(VenueAlias.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.NameKey).HasMaxLength(VenueAlias.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.Kind).HasConversion(EnumColumns.UpperSnake<AliasKind>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(a => a.RemoveReason).HasMaxLength(VenueAlias.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Ignore(a => a.IsRemoved);

        builder.HasIndex(a => a.NameKey).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName(NameKeyIndex);
        builder.HasIndex(a => new { a.TenantId, a.VenueId });
    }
}

/// <summary><c>registry.venue_status_changes</c> (S07 D12): append-only (migration <c>AppendOnly</c>), tenant-owned (RLS).</summary>
internal sealed class VenueStatusChangeConfiguration : IEntityTypeConfiguration<VenueStatusChange>
{
    public const string Table = "venue_status_changes";

    public void Configure(EntityTypeBuilder<VenueStatusChange> builder)
    {
        var statuses = EnumColumns.SqlList<VenueStatus>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_venue_status_changes_from_status", $"from_status IN ({statuses})");
            table.HasCheckConstraint("ck_venue_status_changes_to_status", $"to_status IN ({statuses})");
            table.HasCheckConstraint("ck_venue_status_changes_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });
        builder.IsAppendOnly();

        // The status is PUBLIC; its history and reasons are INTERNAL (S07 D16).
        builder.Property(c => c.FromStatus).HasConversion(EnumColumns.UpperSnake<VenueStatus>()).HasMaxLength(30)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ToStatus).HasConversion(EnumColumns.UpperSnake<VenueStatus>()).HasMaxLength(30)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Reason).HasMaxLength(VenueStatusChange.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.VenueId, c.OccurredAt });
    }
}
