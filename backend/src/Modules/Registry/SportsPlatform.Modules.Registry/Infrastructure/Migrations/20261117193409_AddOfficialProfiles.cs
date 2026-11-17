using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes");

            migrationBuilder.CreateTable(
                name: "official_profiles",
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
                    table.PrimaryKey("pk_official_profiles", x => x.id);
                    table.UniqueConstraint("ak_official_profiles_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_official_profiles_status", "status IN ('ACTIVE', 'INACTIVE', 'RETIRED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_official_profiles_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "official_capabilities",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    official_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    grade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    licence_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    licence_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_official_capabilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_official_capabilities_official_profiles_tenant_id_official_",
                        columns: x => new { x.tenant_id, x.official_profile_id },
                        principalSchema: "registry",
                        principalTable: "official_profiles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes",
                sql: "profile_kind IN ('ATHLETE', 'STAFF', 'OFFICIAL')");

            migrationBuilder.CreateIndex(
                name: "ix_official_capabilities_tenant_id_variant",
                schema: "registry",
                table: "official_capabilities",
                columns: new[] { "tenant_id", "variant" });

            migrationBuilder.CreateIndex(
                name: "ux_official_capabilities_tenant_id_profile_id_variant_role",
                schema: "registry",
                table: "official_capabilities",
                columns: new[] { "tenant_id", "official_profile_id", "variant", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_official_profiles_tenant_id_person_id_open",
                schema: "registry",
                table: "official_profiles",
                columns: new[] { "tenant_id", "person_id" },
                unique: true,
                filter: "status <> 'ARCHIVED'");

            migrationBuilder.EnableTenantRls("registry", "official_profiles");
            migrationBuilder.EnableTenantRls("registry", "official_capabilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "official_capabilities",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "official_profiles",
                schema: "registry");

            migrationBuilder.DropCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_profile_status_changes_profile_kind",
                schema: "registry",
                table: "profile_status_changes",
                sql: "profile_kind IN ('ATHLETE', 'STAFF')");
        }
    }
}
