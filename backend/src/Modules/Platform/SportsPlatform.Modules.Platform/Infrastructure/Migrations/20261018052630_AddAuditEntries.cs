using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                    table.CheckConstraint("ck_audit_entries_actor_kind", "actor_kind IN ('USER', 'SYSTEM')");
                    table.CheckConstraint("ck_audit_entries_outcome", "outcome IN ('SUCCESS', 'FAILURE', 'DENIED')");
                    table.CheckConstraint("ck_audit_entries_source", "source IN ('API', 'SYSTEM')");
                });

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
                name: "ix_audit_entries_correlation_id",
                schema: "audit",
                table: "audit_entries",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_entity_type_entity_id",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_occurred_at",
                schema: "audit",
                table: "audit_entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id_occurred_at",
                schema: "audit",
                table: "audit_entries",
                columns: new[] { "tenant_id", "occurred_at" });

            // AR-19: the runtime roles may only read and insert; triggers reject UPDATE, DELETE and TRUNCATE for all.
            migrationBuilder.AppendOnly("audit", "audit_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "audit");
        }
    }
}
