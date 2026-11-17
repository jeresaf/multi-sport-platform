using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes");

            migrationBuilder.CreateTable(
                name: "staff_profiles",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_profiles", x => x.id);
                    table.UniqueConstraint("ak_staff_profiles_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_staff_profiles_status", "status IN ('ACTIVE', 'INACTIVE', 'RETIRED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_staff_profiles_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_capabilities",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_capabilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_capabilities_staff_profiles_tenant_id_staff_profile_id",
                        columns: x => new { x.tenant_id, x.staff_profile_id },
                        principalSchema: "registry",
                        principalTable: "staff_profiles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes",
                sql: "profile_kind IN ('ATHLETE', 'STAFF')");

            migrationBuilder.CreateIndex(
                name: "ux_staff_capabilities_tenant_id_staff_profile_id_variant_role",
                schema: "registry",
                table: "staff_capabilities",
                columns: new[] { "tenant_id", "staff_profile_id", "variant", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_capabilities_tenant_id_variant",
                schema: "registry",
                table: "staff_capabilities",
                columns: new[] { "tenant_id", "variant" });

            migrationBuilder.CreateIndex(
                name: "ux_staff_profiles_tenant_id_person_id_open",
                schema: "registry",
                table: "staff_profiles",
                columns: new[] { "tenant_id", "person_id" },
                unique: true,
                filter: "status <> 'ARCHIVED'");

            migrationBuilder.EnableTenantRls("registry", "staff_profiles");
            migrationBuilder.EnableTenantRls("registry", "staff_capabilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_capabilities",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "staff_profiles",
                schema: "registry");

            migrationBuilder.DropCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes",
                sql: "profile_kind IN ('ATHLETE')");
        }
    }
}
