using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Identity.Infrastructure.Migrations
{
    /// <summary>Tenant memberships and their role assignments (S03-T4a): tenant-owned, with row-level security.</summary>
    public partial class AddTenantMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status_changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    roles_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => x.id);
                    table.UniqueConstraint("ak_tenant_memberships_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_tenant_memberships_status", "status IN ('ACTIVE', 'SUSPENDED', 'REMOVED')");
                    table.ForeignKey(
                        name: "fk_tenant_memberships_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scope_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_assignments", x => x.id);
                    table.CheckConstraint("ck_role_assignments_revoked", "(revoked_at IS NULL) = (revoked_by IS NULL)");
                    table.CheckConstraint("ck_role_assignments_scope_id", "(scope_type = 'TENANT') = (scope_id IS NULL)");
                    table.CheckConstraint("ck_role_assignments_scope_type", "scope_type IN ('TENANT', 'COMPETITION', 'TEAM', 'CONTEST')");
                    table.ForeignKey(
                        name: "fk_role_assignments_tenant_memberships_tenant_id_membership_id",
                        columns: x => new { x.tenant_id, x.membership_id },
                        principalSchema: "identity",
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_role_assignments_membership_role_scope_active",
                schema: "identity",
                table: "role_assignments",
                columns: new[] { "tenant_id", "membership_id", "role_code", "scope_type", "scope_id" },
                unique: true,
                filter: "revoked_at IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_user_id",
                schema: "identity",
                table: "tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenant_memberships_tenant_id_user_id_current",
                schema: "identity",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "status <> 'REMOVED'");

            migrationBuilder.EnableTenantRls("identity", "tenant_memberships");
            migrationBuilder.EnableTenantRls("identity", "role_assignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_assignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "identity");
        }
    }
}
