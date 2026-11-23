using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.organisations</c> (S07 D4): tenant-owned (RLS). An organisation holds no person's data: what the public
/// reads may show is <c>PUBLIC</c>; the legal name, the body's own email and phone, the keys and the source are
/// <c>INTERNAL</c> (D16). The slug is unique per tenant among every status (D9). The duplicate check (D13) compares the
/// name keys within the same type or city: the type and the city key are indexed per tenant, and the display name key
/// has a trigram index. The primary venue is the tenant's own (composite foreign key); the logo is a Platform asset ID
/// (no foreign key: another module's schema).
/// </summary>
internal sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public const string Table = "organisations";
    public const string SlugIndex = "ux_organisations_tenant_id_slug";
    public const string DisplayNameKeyIndex = "ix_organisations_display_name_key_trgm";
    public const string DirectoryIndex = "ix_organisations_tenant_id_sort_key_id";

    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_organisations_status", $"status IN ({EnumColumns.SqlList<OrganisationStatus>()})");
            table.HasCheckConstraint("ck_organisations_type", $"type IN ({EnumColumns.SqlList<OrganisationType>()})");
            table.HasCheckConstraint("ck_organisations_source", $"source IN ({EnumColumns.SqlList<OrganisationSource>()})");
            table.HasCheckConstraint("ck_organisations_country", "country ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint(
                "ck_organisations_slug",
                $"slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN {Slugs.MinLength} AND {Slugs.MaxLength}");
            table.HasCheckConstraint("ck_organisations_display_name_key", "display_name_key <> ''");
            table.HasCheckConstraint("ck_organisations_sort_key", "sort_key <> ''");
            table.HasCheckConstraint("ck_organisations_founded_year", $"founded_year >= {OrganisationDetails.MinFoundedYear}");
        });

        builder.Property(o => o.Type).HasConversion(EnumColumns.UpperSnake<OrganisationType>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.DisplayName).HasMaxLength(OrganisationDetails.DisplayNameMaxLength)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.DisplayNameKey).HasMaxLength(OrganisationDetails.DisplayNameMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(o => o.LegalName).HasMaxLength(OrganisationDetails.LegalNameMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(o => o.LegalNameKey).HasMaxLength(OrganisationDetails.LegalNameMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(o => o.ShortName).HasMaxLength(OrganisationDetails.ShortNameMaxLength)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.ShortNameKey).HasMaxLength(OrganisationDetails.ShortNameMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);

        // "C": byte order, so the directory sorts and pages the same way in every database, whatever its locale.
        builder.Property(o => o.SortKey).HasMaxLength(OrganisationDetails.DisplayNameMaxLength).UseCollation("C")
            .IsPersonalData(PersonalDataClass.Internal);

        // "C": byte order, as the sort key.
        builder.Property(o => o.Slug).HasMaxLength(Slugs.MaxLength).UseCollation("C").IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.Country).HasMaxLength(2).IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.City).HasMaxLength(OrganisationDetails.CityMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.CityKey).HasMaxLength(OrganisationDetails.CityMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(o => o.FoundedYear).IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.Website).HasMaxLength(OrganisationDetails.WebsiteMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.Email).HasMaxLength(OrganisationDetails.EmailMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(o => o.Phone).HasMaxLength(OrganisationDetails.PhoneMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(o => o.Description).HasMaxLength(OrganisationDetails.DescriptionMaxLength)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.PrimaryVenueId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.LogoAssetId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.Status).HasConversion(EnumColumns.UpperSnake<OrganisationStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(o => o.Source).HasConversion(EnumColumns.UpperSnake<OrganisationSource>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);

        builder.HasAlternateKey(o => new { o.TenantId, o.Id }).HasName("ak_organisations_tenant_id_id");
        builder.HasMany(o => o.StatusHistory).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.OrganisationId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(o => o.ParentLinks).WithOne()
            .HasForeignKey(l => new { l.TenantId, l.OrganisationId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.ParentLinks).HasField("_parentLinks").UsePropertyAccessMode(PropertyAccessMode.Field);

        // The tenant's own venue: a null venue isn't checked (MATCH SIMPLE).
        builder.HasOne<Venue>().WithMany()
            .HasForeignKey(o => new { o.TenantId, o.PrimaryVenueId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.TenantId, o.Slug }).IsUnique().HasDatabaseName(SlugIndex);
        builder.HasIndex(o => new { o.TenantId, o.SortKey, o.Id }).HasDatabaseName(DirectoryIndex);
        builder.HasIndex(o => new { o.TenantId, o.Type });
        builder.HasIndex(o => new { o.TenantId, o.CityKey });
        builder.HasIndex(o => o.DisplayNameKey).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName(DisplayNameKeyIndex);
    }
}

/// <summary>
/// <c>registry.organisation_status_changes</c> (S07 D12): append-only (migration <c>AppendOnly</c>), tenant-owned (RLS).
/// </summary>
internal sealed class OrganisationStatusChangeConfiguration : IEntityTypeConfiguration<OrganisationStatusChange>
{
    public const string Table = "organisation_status_changes";

    public void Configure(EntityTypeBuilder<OrganisationStatusChange> builder)
    {
        var statuses = EnumColumns.SqlList<OrganisationStatus>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_organisation_status_changes_from_status", $"from_status IN ({statuses})");
            table.HasCheckConstraint("ck_organisation_status_changes_to_status", $"to_status IN ({statuses})");
            table.HasCheckConstraint(
                "ck_organisation_status_changes_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });
        builder.IsAppendOnly();

        // The status is PUBLIC; its history and reasons are INTERNAL (S07 D16).
        builder.Property(c => c.FromStatus).HasConversion(EnumColumns.UpperSnake<OrganisationStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ToStatus).HasConversion(EnumColumns.UpperSnake<OrganisationStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Reason).HasMaxLength(OrganisationStatusChange.ReasonMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.OrganisationId, c.OccurredAt });
    }
}

/// <summary>
/// <c>registry.organisation_parent_links</c> (S07 D5): tenant-owned (RLS). Not append-only: ending a link sets
/// <c>valid_to</c>, nothing else changes and nothing is deleted. Both ends are the tenant's organisations (composite
/// foreign keys); at most one link of an organisation is open (partial unique index), and the open links are indexed by
/// parent for the children and the cycle check. The parent is <c>PUBLIC</c> (the public read shows the current one, D17;
/// owner, review #124 Q1); the dates and the reason are <c>INTERNAL</c> (D16: parent-link history and reasons).
/// </summary>
internal sealed class OrganisationParentLinkConfiguration : IEntityTypeConfiguration<OrganisationParentLink>
{
    public const string Table = "organisation_parent_links";
    public const string OpenLinkIndex = "ux_organisation_parent_links_tenant_id_organisation_id_open";

    public void Configure(EntityTypeBuilder<OrganisationParentLink> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_organisation_parent_links_not_self", "organisation_id <> parent_id");
            table.HasCheckConstraint("ck_organisation_parent_links_validity", "valid_to IS NULL OR valid_to >= valid_from");
            table.HasCheckConstraint(
                "ck_organisation_parent_links_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });

        builder.Property(l => l.ParentId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(l => l.ValidFrom).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(l => l.ValidTo).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(l => l.Reason).HasMaxLength(OrganisationParentLink.ReasonMaxLength)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(l => l.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);
        builder.Ignore(l => l.IsOpen);

        // The parent is the tenant's own organisation; the link names it without a navigation.
        builder.HasOne<Organisation>().WithMany()
            .HasForeignKey(l => new { l.TenantId, l.ParentId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.TenantId, l.OrganisationId }).IsUnique().HasFilter("valid_to IS NULL")
            .HasDatabaseName(OpenLinkIndex);
        builder.HasIndex(l => new { l.TenantId, l.OrganisationId, l.SetAt });
        builder.HasIndex(l => new { l.TenantId, l.ParentId }).HasFilter("valid_to IS NULL");
    }
}
