using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <summary>S04-T3a1: tenants' entitlement overrides, tenant-owned (RLS). No tenant has one yet.</summary>
    public partial class AddEntitlementOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_entitlement_overrides",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitlement_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    effective_until = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("pk_tenant_entitlement_overrides", x => x.id);
                    table.CheckConstraint("ck_tenant_entitlement_overrides_entitlement_code", "entitlement_code ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_tenant_entitlement_overrides_removed", "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_tenant_entitlement_overrides_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_entitlement_overrides_unremoved",
                schema: "tenancy",
                table: "tenant_entitlement_overrides",
                columns: new[] { "tenant_id", "entitlement_code" },
                unique: true,
                filter: "removed_at IS NULL");

            migrationBuilder.EnableTenantRls("tenancy", "tenant_entitlement_overrides");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_entitlement_overrides",
                schema: "tenancy");
        }
    }
}
