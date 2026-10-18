using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_next_attempt_at_occurred_at",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.AddColumn<long>(
                name: "sequence",
                schema: "platform",
                table: "outbox_messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate_type_aggregate_id_sequence",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "aggregate_type", "aggregate_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at_sequence",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_aggregate_type_aggregate_id_sequence",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_next_attempt_at_sequence",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "sequence",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at_occurred_at",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at", "occurred_at" });
        }
    }
}
