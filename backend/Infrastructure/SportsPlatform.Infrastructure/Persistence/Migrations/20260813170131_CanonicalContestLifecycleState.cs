using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalContestLifecycleState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalStatus",
                table: "Contests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CanonicalStatusChangedAt",
                table: "Contests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "Contests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContestStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PhaseCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestStatusHistory_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE "Contests"
                SET "CanonicalStatus" = CASE "Status"
                    WHEN 'Ready' THEN 'StartingSoon'
                    WHEN 'InProgress' THEN 'Live'
                    WHEN 'Paused' THEN 'Interrupted'
                    WHEN 'Suspended' THEN 'Suspended'
                    WHEN 'Completed' THEN 'Ended'
                    WHEN 'PendingApproval' THEN 'Ended'
                    WHEN 'Approved' THEN 'Closed'
                    WHEN 'Corrected' THEN 'Closed'
                    WHEN 'Published' THEN 'Closed'
                    WHEN 'Cancelled' THEN 'Cancelled'
                    WHEN 'Abandoned' THEN 'Abandoned'
                    ELSE 'NotStarted'
                END,
                "CanonicalStatusChangedAt" = COALESCE("UpdatedAt", "CreatedAt", NOW());
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CanonicalStatus",
                table: "Contests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CanonicalStatusChangedAt",
                table: "Contests",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "ContestStatusHistory"
                    ("Id", "ContestId", "FromStatus", "ToStatus", "PhaseCode",
                     "ActorId", "Reason", "OccurredAt", "CreatedAt", "UpdatedAt", "TenantId")
                SELECT "Id", "Id", "CanonicalStatus", "CanonicalStatus", NULL,
                       NULL, 'Canonical lifecycle migration backfill',
                       "CanonicalStatusChangedAt", "CanonicalStatusChangedAt", NULL, "TenantId"
                FROM "Contests";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Contests_CanonicalStatus",
                table: "Contests",
                column: "CanonicalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ContestStatusHistory_ContestId_OccurredAt",
                table: "ContestStatusHistory",
                columns: new[] { "ContestId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContestStatusHistory_TenantId",
                table: "ContestStatusHistory",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_Contests_CanonicalStatus",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "CanonicalStatus",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "CanonicalStatusChangedAt",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "Contests");
        }
    }
}
