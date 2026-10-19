using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowAnonymousActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_entries_actor_kind",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_entries_actor_kind",
                schema: "audit",
                table: "audit_entries",
                sql: "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_entries_actor_kind",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_entries_actor_kind",
                schema: "audit",
                table: "audit_entries",
                sql: "actor_kind IN ('USER', 'SYSTEM')");
        }
    }
}
