using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plans",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                    table.CheckConstraint("ck_plans_code", "code ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_plans_rank", "rank >= 1");
                });

            migrationBuilder.CreateTable(
                name: "plan_entitlements",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitlement_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_entitlements", x => x.id);
                    table.CheckConstraint("ck_plan_entitlements_entitlement_code", "entitlement_code ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_plan_entitlements_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "tenancy",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plan_entitlements_plan_id_entitlement_code",
                schema: "tenancy",
                table: "plan_entitlements",
                columns: new[] { "plan_id", "entitlement_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_code",
                schema: "tenancy",
                table: "plans",
                column: "code",
                unique: true);

            // Plans are configuration data: global, written only by the CONFIGURATION seeder as the owner (S04 D2).
            migrationBuilder.ReadOnlyForRuntimeRoles("tenancy", "plans");
            migrationBuilder.ReadOnlyForRuntimeRoles("tenancy", "plan_entitlements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_entitlements",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "tenancy");
        }
    }
}
