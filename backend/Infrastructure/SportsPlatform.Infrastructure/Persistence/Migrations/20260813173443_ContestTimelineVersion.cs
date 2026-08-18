using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContestTimelineVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TimelineVersion",
                table: "Contests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE "Contests" contest
                SET "TimelineVersion" = timeline."EventCount"
                FROM (
                    SELECT source."ContestId", COUNT(*)::bigint AS "EventCount"
                    FROM (
                        SELECT event."ContestId", event."Id" AS "EventId"
                        FROM "ContestEvents" event
                        UNION
                        SELECT score."ContestId", score."EventId"
                        FROM "LiveScores" score
                    ) source
                    GROUP BY source."ContestId"
                ) timeline
                WHERE contest."Id" = timeline."ContestId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimelineVersion",
                table: "Contests");
        }
    }
}
