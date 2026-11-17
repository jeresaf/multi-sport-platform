using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAthleteProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "athlete_profiles",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    primary_position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    other_positions = table.Column<string[]>(type: "text[]", nullable: false),
                    dominant_side = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    public_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_athlete_profiles", x => x.id);
                    table.CheckConstraint("ck_athlete_profiles_dominant_side", "dominant_side IN ('LEFT', 'RIGHT', 'BOTH')");
                    table.CheckConstraint("ck_athlete_profiles_other_positions", "cardinality(other_positions) <= 5");
                    table.CheckConstraint("ck_athlete_profiles_status", "status IN ('ACTIVE', 'INACTIVE', 'RETIRED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_athlete_profiles_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_status_changes",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_profile_status_changes", x => x.id);
                    table.CheckConstraint("ck_profile_status_changes_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_profile_status_changes_from_status", "from_status IN ('ACTIVE', 'INACTIVE', 'RETIRED', 'ARCHIVED')");
                    table.CheckConstraint("ck_profile_status_changes_profile_kind", "profile_kind IN ('ATHLETE')");
                    table.CheckConstraint("ck_profile_status_changes_to_status", "to_status IN ('ACTIVE', 'INACTIVE', 'RETIRED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_profile_status_changes_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_athlete_profiles_tenant_id_variant",
                schema: "registry",
                table: "athlete_profiles",
                columns: new[] { "tenant_id", "variant" });

            migrationBuilder.CreateIndex(
                name: "ux_athlete_profiles_tenant_id_person_id_variant_open",
                schema: "registry",
                table: "athlete_profiles",
                columns: new[] { "tenant_id", "person_id", "variant" },
                unique: true,
                filter: "status <> 'ARCHIVED'");

            migrationBuilder.CreateIndex(
                name: "ix_profile_status_changes_tenant_id_person_id",
                schema: "registry",
                table: "profile_status_changes",
                columns: new[] { "tenant_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_profile_status_changes_tenant_id_profile_kind_profile_id_oc",
                schema: "registry",
                table: "profile_status_changes",
                columns: new[] { "tenant_id", "profile_kind", "profile_id", "occurred_at" });

            migrationBuilder.EnableTenantRls("registry", "athlete_profiles");
            migrationBuilder.EnableTenantRls("registry", "profile_status_changes");
            migrationBuilder.AppendOnly("registry", "profile_status_changes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "athlete_profiles",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "profile_status_changes",
                schema: "registry");
        }
    }
}
