using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers");

            migrationBuilder.AddCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers",
                sql: "entity_type IN ('PERSON', 'VENUE', 'ORGANISATION', 'TEAM')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers");

            // Fails while TEAM identifiers exist: a rollback needs them removed first (no data is deleted here).
            migrationBuilder.AddCheckConstraint(
                name: "ck_external_identifiers_entity_type",
                schema: "registry",
                table: "external_identifiers",
                sql: "entity_type IN ('PERSON', 'VENUE', 'ORGANISATION')");
        }
    }
}
