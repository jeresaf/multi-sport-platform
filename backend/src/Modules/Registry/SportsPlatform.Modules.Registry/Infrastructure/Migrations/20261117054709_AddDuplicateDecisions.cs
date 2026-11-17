using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "duplicate_decisions",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_low_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_high_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_duplicate_decisions", x => x.id);
                    table.CheckConstraint("ck_duplicate_decisions_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_duplicate_decisions_decision", "decision IN ('NOT_SAME')");
                    table.CheckConstraint("ck_duplicate_decisions_pair", "person_low_id < person_high_id");
                    table.CheckConstraint("ck_duplicate_decisions_reason", "btrim(reason) <> ''");
                    table.ForeignKey(
                        name: "fk_duplicate_decisions_person_tenant_id_person_high_id",
                        columns: x => new { x.tenant_id, x.person_high_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_duplicate_decisions_person_tenant_id_person_low_id",
                        columns: x => new { x.tenant_id, x.person_low_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_duplicate_decisions_tenant_id_person_high_id",
                schema: "registry",
                table: "duplicate_decisions",
                columns: new[] { "tenant_id", "person_high_id" });

            migrationBuilder.CreateIndex(
                name: "ux_duplicate_decisions_tenant_id_person_low_id_person_high_id",
                schema: "registry",
                table: "duplicate_decisions",
                columns: new[] { "tenant_id", "person_low_id", "person_high_id" },
                unique: true);

            migrationBuilder.EnableTenantRls("registry", "duplicate_decisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "duplicate_decisions",
                schema: "registry");
        }
    }
}
