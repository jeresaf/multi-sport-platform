using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVenues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "venues",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    short_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "C"),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    address_line = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    surfaces = table.Column<string[]>(type: "character varying(20)[]", nullable: false),
                    supported_variants = table.Column<string[]>(type: "character varying(64)[]", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.id);
                    table.UniqueConstraint("ak_venues_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_venues_capacity", "capacity BETWEEN 0 AND 200000");
                    table.CheckConstraint("ck_venues_coordinates", "(latitude IS NULL AND longitude IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)");
                    table.CheckConstraint("ck_venues_country", "country ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_venues_name_key", "name_key <> ''");
                    table.CheckConstraint("ck_venues_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 2 AND 80");
                    table.CheckConstraint("ck_venues_source", "source IN ('MANUAL', 'SEED')");
                    table.CheckConstraint("ck_venues_status", "status IN ('ACTIVE', 'TEMPORARILY_UNAVAILABLE', 'CLOSED', 'ARCHIVED')");
                    table.CheckConstraint("ck_venues_supported_variants", "cardinality(supported_variants) <= 20");
                    table.CheckConstraint("ck_venues_surfaces", "cardinality(surfaces) <= 5 AND surfaces <@ ARRAY['NATURAL_GRASS', 'ARTIFICIAL_TURF', 'HYBRID_GRASS', 'HARDWOOD', 'SYNTHETIC_COURT', 'CONCRETE', 'CLAY', 'SAND', 'OTHER']::character varying(20)[]");
                    table.CheckConstraint("ck_venues_type", "type IN ('STADIUM', 'ARENA', 'SPORTS_GROUND', 'FIELD', 'COURT', 'INDOOR_HALL', 'TRACK', 'MULTI_SPORT_COMPLEX', 'SCHOOL_GROUND', 'OTHER')");
                });

            migrationBuilder.CreateTable(
                name: "venue_status_changes",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_status_changes", x => x.id);
                    table.CheckConstraint("ck_venue_status_changes_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_venue_status_changes_from_status", "from_status IN ('ACTIVE', 'TEMPORARILY_UNAVAILABLE', 'CLOSED', 'ARCHIVED')");
                    table.CheckConstraint("ck_venue_status_changes_to_status", "to_status IN ('ACTIVE', 'TEMPORARILY_UNAVAILABLE', 'CLOSED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_venue_status_changes_venues_tenant_id_venue_id",
                        columns: x => new { x.tenant_id, x.venue_id },
                        principalSchema: "registry",
                        principalTable: "venues",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_venue_status_changes_tenant_id_venue_id_occurred_at",
                schema: "registry",
                table: "venue_status_changes",
                columns: new[] { "tenant_id", "venue_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_venues_name_key_trgm",
                schema: "registry",
                table: "venues",
                column: "name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_venues_tenant_id_latitude_longitude",
                schema: "registry",
                table: "venues",
                columns: new[] { "tenant_id", "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ux_venues_tenant_id_slug",
                schema: "registry",
                table: "venues",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            migrationBuilder.EnableTenantRls("registry", "venues");
            migrationBuilder.EnableTenantRls("registry", "venue_status_changes");
            migrationBuilder.AppendOnly("registry", "venue_status_changes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "venue_status_changes",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "venues",
                schema: "registry");
        }
    }
}
