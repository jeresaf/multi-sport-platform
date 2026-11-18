using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Domain.Media;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

/// <summary>
/// <c>platform.media_assets</c> (ADR-0014, S06 D16): tenant-owned (RLS), enums as UPPER_SNAKE text, the stored files in
/// <c>variants</c> (<c>jsonb</c>: variant, key, contentType, width, height, bytes). Nothing personal is stored here:
/// the files are reached only through the owning module's checks.
/// </summary>
internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public const string Table = "media_assets";

    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_media_assets_purpose", $"purpose IN ({EnumColumns.SqlList<MediaPurpose>()})");
            table.HasCheckConstraint("ck_media_assets_visibility", $"visibility IN ({EnumColumns.SqlList<MediaVisibility>()})");
            table.HasCheckConstraint("ck_media_assets_status", $"status IN ({EnumColumns.SqlList<MediaAssetStatus>()})");
            table.HasCheckConstraint("ck_media_assets_uploaded_by_kind", $"uploaded_by_kind IN ({EnumColumns.SqlList<ActorKind>()})");
            table.HasCheckConstraint("ck_media_assets_removed_by_kind", $"removed_by_kind IN ({EnumColumns.SqlList<ActorKind>()})");
            table.HasCheckConstraint("ck_media_assets_size_bytes", "size_bytes > 0");
            table.HasCheckConstraint("ck_media_assets_variants", "jsonb_typeof(variants) = 'array' AND jsonb_array_length(variants) > 0");
            table.HasCheckConstraint(
                "ck_media_assets_removed",
                "(status = 'REMOVED') = (removed_at IS NOT NULL AND removed_by_kind IS NOT NULL AND removed_by_id IS NOT NULL)");
        });

        builder.Property(a => a.Purpose).HasConversion(EnumColumns.UpperSnake<MediaPurpose>()).HasMaxLength(30);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.StorageKey).HasMaxLength(200);
        builder.Property(a => a.Visibility).HasConversion(EnumColumns.UpperSnake<MediaVisibility>()).HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion(EnumColumns.UpperSnake<MediaAssetStatus>()).HasMaxLength(20);
        builder.Property(a => a.UploadedByKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);
        builder.Property(a => a.RemovedByKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);
        builder.Property(a => a.Files)
            .HasColumnName("variants")
            .HasColumnType("jsonb")
            .HasConversion(
                files => JsonSerializer.Serialize(files, MessageSerialization.Options),
                json => JsonSerializer.Deserialize<List<MediaAssetFile>>(json, MessageSerialization.Options) ?? new List<MediaAssetFile>(),
                new ValueComparer<IReadOnlyList<MediaAssetFile>>(
                    (left, right) => left!.SequenceEqual(right!),
                    files => files.Aggregate(0, (hash, file) => HashCode.Combine(hash, file)),
                    files => files.ToList()));

        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => new { a.TenantId, a.StorageKey }).IsUnique().HasDatabaseName("ux_media_assets_tenant_id_storage_key");
    }
}
