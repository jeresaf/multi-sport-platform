using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_aliases",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    short_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    remove_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_aliases", x => x.id);
                    table.CheckConstraint("ck_team_aliases_code", "code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_team_aliases_former_name", "(kind = 'FORMER_NAME' AND valid_from IS NOT NULL AND valid_to IS NOT NULL AND valid_from <= valid_to AND removed_at IS NULL) OR (kind <> 'FORMER_NAME' AND valid_from IS NULL AND valid_to IS NULL AND short_name IS NULL AND code IS NULL)");
                    table.CheckConstraint("ck_team_aliases_kind", "kind IN ('FORMER_NAME', 'NICKNAME', 'SPELLING')");
                    table.CheckConstraint("ck_team_aliases_name_key", "name_key <> ''");
                    table.CheckConstraint("ck_team_aliases_removed", "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_team_aliases_teams_tenant_id_team_id",
                        columns: x => new { x.tenant_id, x.team_id },
                        principalSchema: "registry",
                        principalTable: "teams",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_team_aliases_name_key_trgm",
                schema: "registry",
                table: "team_aliases",
                column: "name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_team_aliases_tenant_id_team_id",
                schema: "registry",
                table: "team_aliases",
                columns: new[] { "tenant_id", "team_id" });

            migrationBuilder.EnableTenantRls("registry", "team_aliases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_aliases",
                schema: "registry");
        }
    }
}
