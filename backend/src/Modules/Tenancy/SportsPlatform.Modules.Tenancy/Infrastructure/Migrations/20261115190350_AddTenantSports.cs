using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_sports",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enabled_by = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disabled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    disable_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_sports", x => x.id);
                    table.CheckConstraint("ck_tenant_sports_disabled", "(disabled_at IS NULL AND disabled_by IS NULL AND disable_reason IS NULL) OR (disabled_at IS NOT NULL AND disabled_by IS NOT NULL AND disable_reason IS NOT NULL)");
                    table.CheckConstraint("ck_tenant_sports_variant", "variant ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_tenant_sports_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_sports_open",
                schema: "tenancy",
                table: "tenant_sports",
                columns: new[] { "tenant_id", "variant" },
                unique: true,
                filter: "disabled_at IS NULL");

            migrationBuilder.EnableTenantRls("tenancy", "tenant_sports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_sports",
                schema: "tenancy");
        }
    }
}
