using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFormatDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "format_definitions",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_format_definitions", x => x.id);
                    table.CheckConstraint("ck_format_definitions_code", "code ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_format_definitions_content_hash", "content_hash ~ '^sha256:[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_format_definitions_status", "status IN ('PUBLISHED')");
                    table.CheckConstraint("ck_format_definitions_version", "version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_format_definitions_code_version",
                schema: "config",
                table: "format_definitions",
                columns: new[] { "code", "version" },
                unique: true);

            // Format definitions are configuration data: global, written only by the CONFIGURATION seeder as the owner (S05 D4).
            migrationBuilder.ReadOnlyForRuntimeRoles("config", "format_definitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "format_definitions",
                schema: "config");
        }
    }
}
