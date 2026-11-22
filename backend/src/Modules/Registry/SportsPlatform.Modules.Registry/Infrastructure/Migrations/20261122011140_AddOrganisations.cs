using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organisations",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    display_name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    legal_name_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    short_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    short_name_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    sort_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "C"),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "C"),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    founded_year = table.Column<int>(type: "integer", nullable: true),
                    website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    primary_venue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    logo_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisations", x => x.id);
                    table.UniqueConstraint("ak_organisations_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_organisations_country", "country ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_organisations_display_name_key", "display_name_key <> ''");
                    table.CheckConstraint("ck_organisations_founded_year", "founded_year >= 1800");
                    table.CheckConstraint("ck_organisations_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 2 AND 80");
                    table.CheckConstraint("ck_organisations_sort_key", "sort_key <> ''");
                    table.CheckConstraint("ck_organisations_source", "source IN ('MANUAL', 'SEED')");
                    table.CheckConstraint("ck_organisations_status", "status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'DISSOLVED', 'ARCHIVED')");
                    table.CheckConstraint("ck_organisations_type", "type IN ('CLUB', 'SCHOOL', 'UNIVERSITY', 'ACADEMY', 'FEDERATION', 'ASSOCIATION', 'COMPANY', 'COMMUNITY', 'GOVERNMENT', 'OTHER')");
                    table.ForeignKey(
                        name: "fk_organisations_venue_tenant_id_primary_venue_id",
                        columns: x => new { x.tenant_id, x.primary_venue_id },
                        principalSchema: "registry",
                        principalTable: "venues",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organisation_status_changes",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisation_status_changes", x => x.id);
                    table.CheckConstraint("ck_organisation_status_changes_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_organisation_status_changes_from_status", "from_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'DISSOLVED', 'ARCHIVED')");
                    table.CheckConstraint("ck_organisation_status_changes_to_status", "to_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'DISSOLVED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_organisation_status_changes_organisations_tenant_id_organis",
                        columns: x => new { x.tenant_id, x.organisation_id },
                        principalSchema: "registry",
                        principalTable: "organisations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organisation_status_changes_tenant_id_organisation_id_occur",
                schema: "registry",
                table: "organisation_status_changes",
                columns: new[] { "tenant_id", "organisation_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_organisations_display_name_key_trgm",
                schema: "registry",
                table: "organisations",
                column: "display_name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_organisations_tenant_id_city_key",
                schema: "registry",
                table: "organisations",
                columns: new[] { "tenant_id", "city_key" });

            migrationBuilder.CreateIndex(
                name: "ix_organisations_tenant_id_primary_venue_id",
                schema: "registry",
                table: "organisations",
                columns: new[] { "tenant_id", "primary_venue_id" });

            migrationBuilder.CreateIndex(
                name: "ix_organisations_tenant_id_sort_key_id",
                schema: "registry",
                table: "organisations",
                columns: new[] { "tenant_id", "sort_key", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_organisations_tenant_id_type",
                schema: "registry",
                table: "organisations",
                columns: new[] { "tenant_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ux_organisations_tenant_id_slug",
                schema: "registry",
                table: "organisations",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            migrationBuilder.EnableTenantRls("registry", "organisations");
            migrationBuilder.EnableTenantRls("registry", "organisation_status_changes");
            migrationBuilder.AppendOnly("registry", "organisation_status_changes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organisation_status_changes",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "organisations",
                schema: "registry");
        }
    }
}
