using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teams",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gender_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    age_group = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    short_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    short_name_key = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sort_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "C"),
                    name_since = table.Column<DateOnly>(type: "date", nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "C"),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    motto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    primary_colour = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    secondary_colour = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    tertiary_colour = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    home_venue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    founded_year = table.Column<int>(type: "integer", nullable: true),
                    logo_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    badge_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_teams", x => x.id);
                    table.UniqueConstraint("ak_teams_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_teams_age_group", "age_group IN ('SENIOR', 'U6', 'U7', 'U8', 'U9', 'U10', 'U11', 'U12', 'U13', 'U14', 'U15', 'U16', 'U17', 'U18', 'U19', 'U20', 'U21', 'U22', 'U23', 'VETERANS')");
                    table.CheckConstraint("ck_teams_code", "code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_teams_colour_order", "(secondary_colour IS NULL OR primary_colour IS NOT NULL) AND (tertiary_colour IS NULL OR secondary_colour IS NOT NULL)");
                    table.CheckConstraint("ck_teams_founded_year", "founded_year >= 1800");
                    table.CheckConstraint("ck_teams_gender_category", "gender_category IN ('MEN', 'WOMEN', 'MIXED')");
                    table.CheckConstraint("ck_teams_level", "level IN ('FIRST', 'RESERVE', 'DEVELOPMENT', 'ACADEMY')");
                    table.CheckConstraint("ck_teams_name_key", "name_key <> ''");
                    table.CheckConstraint("ck_teams_name_since", "name_since >= DATE '1800-01-01'");
                    table.CheckConstraint("ck_teams_primary_colour", "primary_colour ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("ck_teams_secondary_colour", "secondary_colour ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("ck_teams_slug", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 2 AND 80");
                    table.CheckConstraint("ck_teams_sort_key", "sort_key <> ''");
                    table.CheckConstraint("ck_teams_source", "source IN ('MANUAL', 'SEED')");
                    table.CheckConstraint("ck_teams_status", "status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'DISBANDED', 'ARCHIVED')");
                    table.CheckConstraint("ck_teams_tertiary_colour", "tertiary_colour ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("ck_teams_variant", "variant <> ''");
                    table.ForeignKey(
                        name: "fk_teams_organisations_tenant_id_organisation_id",
                        columns: x => new { x.tenant_id, x.organisation_id },
                        principalSchema: "registry",
                        principalTable: "organisations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teams_venue_tenant_id_home_venue_id",
                        columns: x => new { x.tenant_id, x.home_venue_id },
                        principalSchema: "registry",
                        principalTable: "venues",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_status_changes",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_team_status_changes", x => x.id);
                    table.CheckConstraint("ck_team_status_changes_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_team_status_changes_from_status", "from_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'DISBANDED', 'ARCHIVED')");
                    table.CheckConstraint("ck_team_status_changes_to_status", "to_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'DISBANDED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_team_status_changes_teams_tenant_id_team_id",
                        columns: x => new { x.tenant_id, x.team_id },
                        principalSchema: "registry",
                        principalTable: "teams",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_team_status_changes_tenant_id_team_id_occurred_at",
                schema: "registry",
                table: "team_status_changes",
                columns: new[] { "tenant_id", "team_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_name_key_trgm",
                schema: "registry",
                table: "teams",
                column: "name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id_home_venue_id",
                schema: "registry",
                table: "teams",
                columns: new[] { "tenant_id", "home_venue_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id_organisation_id",
                schema: "registry",
                table: "teams",
                columns: new[] { "tenant_id", "organisation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id_sort_key_id",
                schema: "registry",
                table: "teams",
                columns: new[] { "tenant_id", "sort_key", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id_status",
                schema: "registry",
                table: "teams",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id_variant",
                schema: "registry",
                table: "teams",
                columns: new[] { "tenant_id", "variant" });

            migrationBuilder.CreateIndex(
                name: "ux_teams_tenant_id_slug",
                schema: "registry",
                table: "teams",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            migrationBuilder.EnableTenantRls("registry", "teams");
            migrationBuilder.EnableTenantRls("registry", "team_status_changes");
            migrationBuilder.AppendOnly("registry", "team_status_changes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_status_changes",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "teams",
                schema: "registry");
        }
    }
}
