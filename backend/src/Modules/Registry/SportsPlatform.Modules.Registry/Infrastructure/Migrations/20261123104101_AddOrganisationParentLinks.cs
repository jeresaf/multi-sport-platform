using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationParentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organisation_parent_links",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organisation_parent_links", x => x.id);
                    table.CheckConstraint("ck_organisation_parent_links_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_organisation_parent_links_not_self", "organisation_id <> parent_id");
                    table.CheckConstraint("ck_organisation_parent_links_validity", "valid_to IS NULL OR valid_to >= valid_from");
                    table.ForeignKey(
                        name: "fk_organisation_parent_links_organisations_tenant_id_organisat",
                        columns: x => new { x.tenant_id, x.organisation_id },
                        principalSchema: "registry",
                        principalTable: "organisations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_organisation_parent_links_organisations_tenant_id_parent_id",
                        columns: x => new { x.tenant_id, x.parent_id },
                        principalSchema: "registry",
                        principalTable: "organisations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organisation_parent_links_tenant_id_organisation_id_set_at",
                schema: "registry",
                table: "organisation_parent_links",
                columns: new[] { "tenant_id", "organisation_id", "set_at" });

            migrationBuilder.CreateIndex(
                name: "ix_organisation_parent_links_tenant_id_parent_id",
                schema: "registry",
                table: "organisation_parent_links",
                columns: new[] { "tenant_id", "parent_id" },
                filter: "valid_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_organisation_parent_links_tenant_id_organisation_id_open",
                schema: "registry",
                table: "organisation_parent_links",
                columns: new[] { "tenant_id", "organisation_id" },
                unique: true,
                filter: "valid_to IS NULL");

            migrationBuilder.EnableTenantRls("registry", "organisation_parent_links");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organisation_parent_links",
                schema: "registry");
        }
    }
}
