using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.IntegrationTests.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProbes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_probes",
                schema: "conventions_test",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_probes", x => x.id);
                    table.UniqueConstraint("ak_tenant_probes_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "tenant_probe_notes",
                schema: "conventions_test",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    probe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_probe_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_probe_notes_tenant_probes_tenant_id_probe_id",
                        columns: x => new { x.tenant_id, x.probe_id },
                        principalSchema: "conventions_test",
                        principalTable: "tenant_probes",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_probe_notes_tenant_id_probe_id",
                schema: "conventions_test",
                table: "tenant_probe_notes",
                columns: new[] { "tenant_id", "probe_id" });

            migrationBuilder.EnableTenantRls("conventions_test", "tenant_probes");
            migrationBuilder.EnableTenantRls("conventions_test", "tenant_probe_notes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_probe_notes",
                schema: "conventions_test");

            migrationBuilder.DropTable(
                name: "tenant_probes",
                schema: "conventions_test");
        }
    }
}
