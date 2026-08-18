using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TimelineEventPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryPlayerId",
                table: "LiveScores",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecondaryPlayerId",
                table: "LiveScores",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "LiveScores" score
                SET "PrimaryPlayerId" = score."SecondaryParticipantId"
                WHERE score."SecondaryParticipantId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "ContestParticipants" participant
                      WHERE participant."ContestId" = score."ContestId"
                        AND participant."ParticipantId" = score."SecondaryParticipantId"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimaryPlayerId",
                table: "LiveScores");

            migrationBuilder.DropColumn(
                name: "SecondaryPlayerId",
                table: "LiveScores");
        }
    }
}
