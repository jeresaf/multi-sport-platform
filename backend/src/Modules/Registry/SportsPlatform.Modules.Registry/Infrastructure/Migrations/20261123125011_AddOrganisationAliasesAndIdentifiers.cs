using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationAliasesAndIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers");

            migrationBuilder.CreateTable(
                name: "organisation_aliases",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
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
                    table.PrimaryKey("pk_organisation_aliases", x => x.id);
                    table.CheckConstraint("ck_organisation_aliases_kind", "kind IN ('FORMER_NAME', 'NICKNAME', 'SPELLING')");
                    table.CheckConstraint("ck_organisation_aliases_name_key", "name_key <> ''");
                    table.CheckConstraint("ck_organisation_aliases_removed", "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_organisation_aliases_organisations_tenant_id_organisation_id",
                        columns: x => new { x.tenant_id, x.organisation_id },
                        principalSchema: "registry",
                        principalTable: "organisations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers",
                sql: "entity_type IN ('PERSON', 'VENUE', 'ORGANISATION')");

            migrationBuilder.CreateIndex(
                name: "ix_organisation_aliases_name_key_trgm",
                schema: "registry",
                table: "organisation_aliases",
                column: "name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_organisation_aliases_tenant_id_organisation_id",
                schema: "registry",
                table: "organisation_aliases",
                columns: new[] { "tenant_id", "organisation_id" });

            migrationBuilder.EnableTenantRls("registry", "organisation_aliases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organisation_aliases",
                schema: "registry");

            migrationBuilder.DropCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers");

            // Fails while ORGANISATION identifiers exist: a rollback needs them removed first (no data is deleted here).
            migrationBuilder.AddCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers",
                sql: "entity_type IN ('PERSON', 'VENUE')");
        }
    }
}
