using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.teams</c> (S07 D6, D7): tenant-owned (RLS). A team holds no person's data: what the public reads may show
/// is <c>PUBLIC</c>; the keys and the source are <c>INTERNAL</c> (D16). The slug is unique per tenant
/// among every status (D9). The duplicate check (D13) compares teams of the same variant and category: it narrows on
/// the variant (indexed per tenant) and measures similarity within those rows, so it doesn't use the name key's trigram
/// index; that index is for the directory's search (S07-T3b-2), as organisations' (review #127 R4). The organisation and the home venue are the tenant's own (composite
/// foreign keys); the variant is a Sports Configuration code and the logo and badge are Platform asset IDs (no foreign
/// keys: other modules' schemas).
/// </summary>
internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public const string Table = "teams";
    public const string SlugIndex = "ux_teams_tenant_id_slug";
    public const string NameKeyIndex = "ix_teams_name_key_trgm";
    public const string DirectoryIndex = "ix_teams_tenant_id_sort_key_id";

    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_teams_status", $"status IN ({EnumColumns.SqlList<TeamStatus>()})");
            table.HasCheckConstraint("ck_teams_source", $"source IN ({EnumColumns.SqlList<TeamSource>()})");
            table.HasCheckConstraint("ck_teams_gender_category", $"gender_category IN ({EnumColumns.SqlList<GenderCategory>()})");
            table.HasCheckConstraint("ck_teams_age_group", $"age_group IN ({EnumColumns.SqlList<AgeGroup>()})");
            table.HasCheckConstraint("ck_teams_level", $"level IN ({EnumColumns.SqlList<TeamLevel>()})");
            table.HasCheckConstraint(
                "ck_teams_slug",
                $"slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN {Slugs.MinLength} AND {Slugs.MaxLength}");
            table.HasCheckConstraint("ck_teams_variant", "variant <> ''");
            table.HasCheckConstraint("ck_teams_name_key", "name_key <> ''");
            table.HasCheckConstraint("ck_teams_sort_key", "sort_key <> ''");
            table.HasCheckConstraint("ck_teams_code", "code ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("ck_teams_primary_colour", "primary_colour ~ '^#[0-9A-F]{6}$'");
            table.HasCheckConstraint("ck_teams_secondary_colour", "secondary_colour ~ '^#[0-9A-F]{6}$'");
            table.HasCheckConstraint("ck_teams_tertiary_colour", "tertiary_colour ~ '^#[0-9A-F]{6}$'");
            table.HasCheckConstraint(
                "ck_teams_colour_order",
                "(secondary_colour IS NULL OR primary_colour IS NOT NULL) AND (tertiary_colour IS NULL OR secondary_colour IS NOT NULL)");
            table.HasCheckConstraint("ck_teams_founded_year", $"founded_year >= {TeamDetails.MinFoundedYear}");
            table.HasCheckConstraint("ck_teams_name_since", $"name_since >= DATE '{TeamDetails.MinNameSince:yyyy-MM-dd}'");
        });

        builder.Property(t => t.Variant).HasMaxLength(TeamDetails.VariantMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.GenderCategory).HasConversion(EnumColumns.UpperSnake<GenderCategory>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.AgeGroup).HasConversion(EnumColumns.UpperSnake<AgeGroup>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.Level).HasConversion(EnumColumns.UpperSnake<TeamLevel>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Ignore(t => t.Category);
        builder.Property(t => t.OrganisationId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.Name).HasMaxLength(TeamDetails.NameMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.NameKey).HasMaxLength(TeamDetails.NameMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(t => t.ShortName).HasMaxLength(TeamDetails.ShortNameMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.ShortNameKey).HasMaxLength(TeamDetails.ShortNameMaxLength).IsPersonalData(PersonalDataClass.Internal);

        // "C": byte order, so the directory sorts and pages the same way in every database, whatever its locale.
        builder.Property(t => t.SortKey).HasMaxLength(TeamDetails.NameMaxLength).UseCollation("C")
            .IsPersonalData(PersonalDataClass.Internal);

        // When the current name began: part of the names' dates the public reads show (D16).
        builder.Property(t => t.NameSince).IsPersonalData(PersonalDataClass.Public);

        // "C": byte order, as the sort key.
        builder.Property(t => t.Slug).HasMaxLength(Slugs.MaxLength).UseCollation("C").IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.Code).HasMaxLength(TeamDetails.CodeLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.Motto).HasMaxLength(TeamDetails.MottoMaxLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.PrimaryColour).HasMaxLength(TeamDetails.ColourLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.SecondaryColour).HasMaxLength(TeamDetails.ColourLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.TertiaryColour).HasMaxLength(TeamDetails.ColourLength).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.HomeVenueId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.FoundedYear).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.LogoAssetId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.BadgeAssetId).IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.Status).HasConversion(EnumColumns.UpperSnake<TeamStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Public);
        builder.Property(t => t.Source).HasConversion(EnumColumns.UpperSnake<TeamSource>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);

        builder.HasAlternateKey(t => new { t.TenantId, t.Id }).HasName("ak_teams_tenant_id_id");
        builder.HasMany(t => t.StatusHistory).WithOne()
            .HasForeignKey(c => new { c.TenantId, c.TeamId })
            .HasPrincipalKey(t => new { t.TenantId, t.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);

        // The tenant's own organisation and venue: a null one isn't checked (MATCH SIMPLE).
        builder.HasOne<Organisation>().WithMany()
            .HasForeignKey(t => new { t.TenantId, t.OrganisationId })
            .HasPrincipalKey(o => new { o.TenantId, o.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Venue>().WithMany()
            .HasForeignKey(t => new { t.TenantId, t.HomeVenueId })
            .HasPrincipalKey(v => new { v.TenantId, v.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.TenantId, t.Slug }).IsUnique().HasDatabaseName(SlugIndex);
        builder.HasIndex(t => new { t.TenantId, t.SortKey, t.Id }).HasDatabaseName(DirectoryIndex);
        builder.HasIndex(t => new { t.TenantId, t.Variant });
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => t.NameKey).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName(NameKeyIndex);
    }
}

/// <summary><c>registry.team_status_changes</c> (S07 D12): append-only (migration <c>AppendOnly</c>), tenant-owned (RLS).</summary>
internal sealed class TeamStatusChangeConfiguration : IEntityTypeConfiguration<TeamStatusChange>
{
    public const string Table = "team_status_changes";

    public void Configure(EntityTypeBuilder<TeamStatusChange> builder)
    {
        var statuses = EnumColumns.SqlList<TeamStatus>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_team_status_changes_from_status", $"from_status IN ({statuses})");
            table.HasCheckConstraint("ck_team_status_changes_to_status", $"to_status IN ({statuses})");
            table.HasCheckConstraint("ck_team_status_changes_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });
        builder.IsAppendOnly();

        // The status is PUBLIC; its history and reasons are INTERNAL (S07 D16).
        builder.Property(c => c.FromStatus).HasConversion(EnumColumns.UpperSnake<TeamStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ToStatus).HasConversion(EnumColumns.UpperSnake<TeamStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.Reason).HasMaxLength(TeamStatusChange.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasIndex(c => new { c.TenantId, c.TeamId, c.OccurredAt });
    }
}
