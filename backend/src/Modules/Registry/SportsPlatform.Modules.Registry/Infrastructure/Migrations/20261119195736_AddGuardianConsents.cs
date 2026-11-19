using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guardian_consents",
                schema: "registry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scopes = table.Column<string[]>(type: "character varying(20)[]", nullable: false),
                    given_on = table.Column<DateOnly>(type: "date", nullable: false),
                    evidence_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    replaces_consent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardian_consents", x => x.id);
                    table.UniqueConstraint("ak_guardian_consents_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_guardian_consents_relationship", "relationship IN ('MOTHER', 'FATHER', 'GUARDIAN', 'OTHER')");
                    table.CheckConstraint("ck_guardian_consents_scopes", "cardinality(scopes) BETWEEN 1 AND 3 AND scopes <@ ARRAY['DATA_PROCESSING', 'PUBLIC_PROFILE', 'PHOTO']::character varying(20)[]");
                    table.CheckConstraint("ck_guardian_consents_status", "status IN ('ACTIVE', 'WITHDRAWN')");
                    table.CheckConstraint("ck_guardian_consents_withdrawn", "(status = 'ACTIVE' AND withdrawn_at IS NULL AND withdrawn_by IS NULL AND withdrawal_reason IS NULL) OR (status = 'WITHDRAWN' AND withdrawn_at IS NOT NULL AND withdrawn_by IS NOT NULL AND withdrawal_reason IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_guardian_consents_persons_tenant_id_person_id",
                        columns: x => new { x.tenant_id, x.person_id },
                        principalSchema: "registry",
                        principalTable: "persons",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_guardian_consents_replaces_consent_id",
                        columns: x => new { x.tenant_id, x.replaces_consent_id },
                        principalSchema: "registry",
                        principalTable: "guardian_consents",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guardian_consents_tenant_id_person_id",
                schema: "registry",
                table: "guardian_consents",
                columns: new[] { "tenant_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "ix_guardian_consents_tenant_id_replaces_consent_id",
                schema: "registry",
                table: "guardian_consents",
                columns: new[] { "tenant_id", "replaces_consent_id" });

            migrationBuilder.EnableTenantRls("registry", "guardian_consents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guardian_consents",
                schema: "registry");
        }
    }
}
