using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalTimelineSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TimelineSequence",
                table: "LiveScores",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TimelineSequence",
                table: "ContestEvents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                CREATE TEMP TABLE canonical_timeline_sequences ON COMMIT DROP AS
                WITH unified AS (
                    SELECT source."ContestId", source."EventId", MIN(source."RecordedAt") AS "RecordedAt"
                    FROM (
                        SELECT event."ContestId", event."Id" AS "EventId", event."ServerTimestamp" AS "RecordedAt"
                        FROM "ContestEvents" event
                        UNION ALL
                        SELECT score."ContestId", score."EventId", score."ServerTimestamp" AS "RecordedAt"
                        FROM "LiveScores" score
                    ) source
                    GROUP BY source."ContestId", source."EventId"
                )
                SELECT unified."ContestId", unified."EventId",
                    ROW_NUMBER() OVER (
                        PARTITION BY unified."ContestId"
                        ORDER BY unified."RecordedAt", unified."EventId")::bigint AS "Sequence"
                FROM unified;

                UPDATE "ContestEvents" event
                SET "TimelineSequence" = sequence."Sequence"
                FROM canonical_timeline_sequences sequence
                WHERE event."ContestId" = sequence."ContestId"
                  AND event."Id" = sequence."EventId";

                UPDATE "LiveScores" score
                SET "TimelineSequence" = sequence."Sequence"
                FROM canonical_timeline_sequences sequence
                WHERE score."ContestId" = sequence."ContestId"
                  AND score."EventId" = sequence."EventId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LiveScores_ContestId_TimelineSequence",
                table: "LiveScores",
                columns: new[] { "ContestId", "TimelineSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestEvents_ContestId_TimelineSequence",
                table: "ContestEvents",
                columns: new[] { "ContestId", "TimelineSequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LiveScores_ContestId_TimelineSequence",
                table: "LiveScores");

            migrationBuilder.DropIndex(
                name: "IX_ContestEvents_ContestId_TimelineSequence",
                table: "ContestEvents");

            migrationBuilder.DropColumn(
                name: "TimelineSequence",
                table: "LiveScores");

            migrationBuilder.DropColumn(
                name: "TimelineSequence",
                table: "ContestEvents");
        }
    }
}
