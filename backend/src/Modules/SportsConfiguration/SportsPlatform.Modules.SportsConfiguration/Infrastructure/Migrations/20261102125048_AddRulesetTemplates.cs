using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRulesetTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ruleset_templates",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sport_variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sport_version = table.Column<int>(type: "integer", nullable: false),
                    formats = table.Column<List<string>>(type: "text[]", nullable: false),
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
                    table.PrimaryKey("pk_ruleset_templates", x => x.id);
                    table.CheckConstraint("ck_ruleset_templates_code", "code ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_ruleset_templates_content_hash", "content_hash ~ '^sha256:[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_ruleset_templates_formats", "cardinality(formats) >= 1");
                    table.CheckConstraint("ck_ruleset_templates_sport_variant", "sport_variant ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_ruleset_templates_sport_version", "sport_version >= 1");
                    table.CheckConstraint("ck_ruleset_templates_status", "status IN ('PUBLISHED')");
                    table.CheckConstraint("ck_ruleset_templates_version", "version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ruleset_templates_code_version",
                schema: "config",
                table: "ruleset_templates",
                columns: new[] { "code", "version" },
                unique: true);

            // Ruleset templates are configuration data: global, written only by the CONFIGURATION seeder as the owner (S05 D4).
            migrationBuilder.ReadOnlyForRuntimeRoles("config", "ruleset_templates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ruleset_templates",
                schema: "config");
        }
    }
}
