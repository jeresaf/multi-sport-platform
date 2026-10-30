using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsPlatform.Modules.SportsConfiguration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSportDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "config");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "config",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => new { x.message_id, x.consumer });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    event_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_version = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_messages_status", "status IN ('PENDING', 'PROCESSED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "sport_definitions",
                schema: "config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sport = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scoring_family = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sport_definitions", x => x.id);
                    table.CheckConstraint("ck_sport_definitions_content_hash", "content_hash ~ '^sha256:[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_sport_definitions_scoring_family", "scoring_family ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_sport_definitions_shape", "shape IN ('HEAD_TO_HEAD', 'FIELD')");
                    table.CheckConstraint("ck_sport_definitions_sport", "sport ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_sport_definitions_status", "status IN ('PUBLISHED')");
                    table.CheckConstraint("ck_sport_definitions_variant", "variant ~ '^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$'");
                    table.CheckConstraint("ck_sport_definitions_version", "version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate_type_aggregate_id_sequence",
                schema: "config",
                table: "outbox_messages",
                columns: new[] { "aggregate_type", "aggregate_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at_sequence",
                schema: "config",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_sport_definitions_variant_version",
                schema: "config",
                table: "sport_definitions",
                columns: new[] { "variant", "version" },
                unique: true);

            // Sport definitions are configuration data: global, written only by the CONFIGURATION seeder as the owner (S05 D4).
            migrationBuilder.ReadOnlyForRuntimeRoles("config", "sport_definitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "config");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "config");

            migrationBuilder.DropTable(
                name: "sport_definitions",
                schema: "config");
        }
    }
}
