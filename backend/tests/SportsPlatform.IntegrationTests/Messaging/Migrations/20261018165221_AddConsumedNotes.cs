using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.IntegrationTests.Messaging.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumedNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumed_notes",
                schema: "messaging_test",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumed_notes", x => x.id);
                });

            migrationBuilder.AppendOnly("messaging_test", "consumed_notes");
            migrationBuilder.EnableTenantRls("messaging_test", "consumed_notes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumed_notes",
                schema: "messaging_test");
        }
    }
}
