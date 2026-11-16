using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.IntegrationTests.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProbeNoteMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_tenant_probe_notes_tenant_id_id",
                schema: "conventions_test",
                table: "tenant_probe_notes",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "tenant_probe_note_marks",
                schema: "conventions_test",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_probe_note_marks", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_probe_note_marks_tenant_probe_notes_tenant_id_note_id",
                        columns: x => new { x.tenant_id, x.note_id },
                        principalSchema: "conventions_test",
                        principalTable: "tenant_probe_notes",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_probe_note_marks_tenant_id_note_id",
                schema: "conventions_test",
                table: "tenant_probe_note_marks",
                columns: new[] { "tenant_id", "note_id" });

            migrationBuilder.EnableTenantRls("conventions_test", "tenant_probe_note_marks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_probe_note_marks",
                schema: "conventions_test");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_tenant_probe_notes_tenant_id_id",
                schema: "conventions_test",
                table: "tenant_probe_notes");
        }
    }
}
