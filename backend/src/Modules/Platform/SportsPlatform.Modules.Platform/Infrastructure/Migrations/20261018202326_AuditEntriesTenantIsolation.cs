using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditEntriesTenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_entries_action_code_occurred_at",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_actor_id_occurred_at",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_entity_type_entity_id",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_entity_type_entity_id_occurred_at",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id_action_code_occurred_at",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "tenant_id", "action_code", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id_actor_id_occurred_at",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "tenant_id", "actor_id", "occurred_at" });

            // sp_app sees its tenant's entries only; entries without a tenant only through sp_platform (S02 D3).
            // TEMPORARY (S02-T2b acceptance): no policy, so RlsCoverageTests must fail. Reverted next.
            // migrationBuilder.EnableTenantRls("audit", "audit_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("audit", "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_entity_type_entity_id_occurred_at",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_tenant_id_action_code_occurred_at",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_tenant_id_actor_id_occurred_at",
                schema: "audit",
                table: "audit_entries");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_action_code_occurred_at",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "action_code", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_actor_id_occurred_at",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_entity_type_entity_id",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "entity_type", "entity_id" });
        }
    }
}
