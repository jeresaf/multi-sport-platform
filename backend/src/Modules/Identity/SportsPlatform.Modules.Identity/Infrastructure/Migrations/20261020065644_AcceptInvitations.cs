using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcceptInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "accepted_at",
                schema: "identity",
                table: "invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "accepted_by",
                schema: "identity",
                table: "invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "membership_id",
                schema: "identity",
                table: "invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_tenant_id_membership_id",
                schema: "identity",
                table: "invitations",
                columns: new[] { "tenant_id", "membership_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_accepted",
                schema: "identity",
                table: "invitations",
                sql: "(status = 'ACCEPTED') = (accepted_by IS NOT NULL AND accepted_at IS NOT NULL AND membership_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_invitations_tenant_membership_tenant_id_membership_id",
                schema: "identity",
                table: "invitations",
                columns: new[] { "tenant_id", "membership_id" },
                principalSchema: "identity",
                principalTable: "tenant_memberships",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_invitations_tenant_membership_tenant_id_membership_id",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropIndex(
                name: "ix_invitations_tenant_id_membership_id",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_accepted",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "accepted_at",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "accepted_by",
                schema: "identity",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "membership_id",
                schema: "identity",
                table: "invitations");
        }
    }
}
