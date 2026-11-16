using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRegistrySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "registry");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "external_identifiers",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_identifiers", x => x.id);
                    table.CheckConstraint("ck_external_identifiers_authority", "authority ~ '^[A-Z][A-Z0-9_]{1,39}$'");
                    table.CheckConstraint("ck_external_identifiers_entity_type", "entity_type IN ('PERSON')");
                    table.CheckConstraint("ck_external_identifiers_validity", "valid_from IS NULL OR valid_to IS NULL OR valid_to >= valid_from");
                    table.CheckConstraint("ck_external_identifiers_value", "value <> '' AND value = btrim(value)");
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "registry",
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
                schema: "registry",
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
                name: "persons",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    other_names = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    first_name_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "C"),
                    last_name_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "C"),
                    name_key = table.Column<string>(type: "character varying(503)", maxLength: 503, nullable: false),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    nationality = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    secondary_nationality = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    height_cm = table.Column<int>(type: "integer", nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    photo_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_profile_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    import_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_persons", x => x.id);
                    table.UniqueConstraint("ak_persons_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_persons_gender", "gender IN ('FEMALE', 'MALE')");
                    table.CheckConstraint("ck_persons_height_cm", "height_cm BETWEEN 50 AND 260");
                    table.CheckConstraint("ck_persons_name_keys", "first_name_key <> '' AND last_name_key <> '' AND name_key <> ''");
                    table.CheckConstraint("ck_persons_nationality", "nationality ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_persons_secondary_nationality", "secondary_nationality ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_persons_source", "source IN ('MANUAL', 'SEED')");
                    table.CheckConstraint("ck_persons_status", "status IN ('ACTIVE', 'INACTIVE', 'DECEASED', 'ARCHIVED')");
                    table.CheckConstraint("ck_persons_weight_kg", "weight_kg BETWEEN 10 AND 300");
                });

            migrationBuilder.CreateTable(
                name: "person_aliases",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    remove_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_aliases", x => x.id);
                    table.CheckConstraint("ck_person_aliases_kind", "kind IN ('FORMER_NAME', 'NICKNAME', 'SPELLING')");
                    table.CheckConstraint("ck_person_aliases_name_key", "name_key <> ''");
                    table.CheckConstraint("ck_person_aliases_removed", "(removed_at IS NULL AND removed_by IS NULL AND remove_reason IS NULL) OR (removed_at IS NOT NULL AND removed_by IS NOT NULL AND remove_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_person_aliases_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_status_changes",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_status_changes", x => x.id);
                    table.CheckConstraint("ck_person_status_changes_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'ANONYMOUS')");
                    table.CheckConstraint("ck_person_status_changes_from_status", "from_status IN ('ACTIVE', 'INACTIVE', 'DECEASED', 'ARCHIVED')");
                    table.CheckConstraint("ck_person_status_changes_to_status", "to_status IN ('ACTIVE', 'INACTIVE', 'DECEASED', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_person_status_changes_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_identifiers_tenant_id_entity_type_entity_id",
                schema: "registry",
                table: "external_identifiers",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ux_external_identifiers_tenant_id_authority_entity_type_value",
                schema: "registry",
                table: "external_identifiers",
                columns: new[] { "tenant_id", "authority", "entity_type", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate_type_aggregate_id_sequence",
                schema: "registry",
                table: "outbox_messages",
                columns: new[] { "aggregate_type", "aggregate_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at_sequence",
                schema: "registry",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_person_aliases_name_key_trgm",
                schema: "registry",
                table: "person_aliases",
                column: "name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_person_aliases_tenant_id_person_id",
                schema: "registry",
                table: "person_aliases",
                columns: new[] { "tenant_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_person_status_changes_tenant_id_person_id_occurred_at",
                schema: "registry",
                table: "person_status_changes",
                columns: new[] { "tenant_id", "person_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_persons_name_key_trgm",
                schema: "registry",
                table: "persons",
                column: "name_key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_persons_tenant_id_last_name_key_first_name_key_id",
                schema: "registry",
                table: "persons",
                columns: new[] { "tenant_id", "last_name_key", "first_name_key", "id" });

            migrationBuilder.EnableTenantRls("registry", "persons");
            migrationBuilder.EnableTenantRls("registry", "person_aliases");
            migrationBuilder.EnableTenantRls("registry", "person_status_changes");
            migrationBuilder.EnableTenantRls("registry", "external_identifiers");
            migrationBuilder.AppendOnly("registry", "person_status_changes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_identifiers",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "person_aliases",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "person_status_changes",
                schema: "registry");

            migrationBuilder.DropTable(
                name: "persons",
                schema: "registry");
        }
    }
}
