using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowAnonymousActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_status_history_actor_kind",
                schema: "tenancy",
                table: "tenant_status_history");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_status_history_actor_kind",
                schema: "tenancy",
                table: "tenant_status_history",
                sql: "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_status_history_actor_kind",
                schema: "tenancy",
                table: "tenant_status_history");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_status_history_actor_kind",
                schema: "tenancy",
                table: "tenant_status_history",
                sql: "actor_kind IN ('USER', 'SYSTEM')");
        }
    }
}
