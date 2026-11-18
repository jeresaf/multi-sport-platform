using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    variants = table.Column<string>(type: "jsonb", nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uploaded_by_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_by_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    removed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_assets_purpose", "purpose IN ('PERSON_PHOTO', 'CONSENT_EVIDENCE')");
                    table.CheckConstraint("ck_media_assets_removed", "(status = 'REMOVED') = (removed_at IS NOT NULL AND removed_by_kind IS NOT NULL AND removed_by_id IS NOT NULL)");
                    table.CheckConstraint("ck_media_assets_removed_by_kind", "removed_by_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_media_assets_size_bytes", "size_bytes > 0");
                    table.CheckConstraint("ck_media_assets_status", "status IN ('ACTIVE', 'REMOVED')");
                    table.CheckConstraint("ck_media_assets_uploaded_by_kind", "uploaded_by_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_media_assets_variants", "jsonb_typeof(variants) = 'array' AND jsonb_array_length(variants) > 0");
                    table.CheckConstraint("ck_media_assets_visibility", "visibility IN ('PRIVATE', 'PUBLIC')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_tenant_id_status",
                schema: "platform",
                table: "media_assets",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_media_assets_tenant_id_storage_key",
                schema: "platform",
                table: "media_assets",
                columns: new[] { "tenant_id", "storage_key" },
                unique: true);

            migrationBuilder.EnableTenantRls("platform", "media_assets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "platform");
        }
    }
}
