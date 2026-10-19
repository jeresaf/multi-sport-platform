using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Migrations
{
    /// <summary>
    /// S02-T4a: the tenant's settings move to <c>tenancy.tenant_settings</c>, the first tenant-owned business table
    /// (RLS). Existing tenants' time zone, locale, currency and public flag are copied before the columns are dropped.
    /// </summary>
    public partial class MoveSettingsToTenantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_settings",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    default_locale = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    public_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    starting_soon_minutes = table.Column<int>(type: "integer", nullable: false),
                    minor_registration_consent = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_settings", x => x.id);
                    table.CheckConstraint("ck_tenant_settings_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_tenant_settings_default_locale", "default_locale ~ '^[a-z]{2}(-[A-Z]{2})?$'");
                    table.CheckConstraint("ck_tenant_settings_minor_registration_consent", "minor_registration_consent IN ('REQUIRED', 'RECOMMENDED', 'OFF')");
                    table.CheckConstraint("ck_tenant_settings_starting_soon_minutes", "starting_soon_minutes BETWEEN 5 AND 180");
                    table.ForeignKey(
                        name: "fk_tenant_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_settings_tenant_id",
                schema: "tenancy",
                table: "tenant_settings",
                column: "tenant_id",
                unique: true);

            // One row per existing tenant, with its values and the defaults for the new keys. The ID is the tenant's
            // (a UUIDv7, unique per row); the audit columns are the tenant's, since the settings existed from its
            // creation. Before RLS: the migrator has no tenant set, so forced RLS would refuse these rows.
            migrationBuilder.Sql("""
                INSERT INTO tenancy.tenant_settings (
                    id, tenant_id, time_zone, default_locale, currency_code, public_enabled, starting_soon_minutes,
                    minor_registration_consent, created_at, created_by, updated_at, updated_by)
                SELECT id, id, time_zone, default_locale, currency_code, public_enabled, 30, 'RECOMMENDED',
                    created_at, created_by, updated_at, updated_by
                FROM tenancy.tenants;
                """);

            migrationBuilder.EnableTenantRls("tenancy", "tenant_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_currency_code",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenants_default_locale",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "default_locale",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "public_enabled",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "time_zone",
                schema: "tenancy",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "default_locale",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "public_enabled",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Back onto the tenants, without RLS so the migrator sees every row; the columns' temporary defaults go and the
            // check constraints come after.
            migrationBuilder.DisableTenantRls("tenancy", "tenant_settings");
            migrationBuilder.Sql("""
                UPDATE tenancy.tenants t
                SET time_zone = s.time_zone, default_locale = s.default_locale, currency_code = s.currency_code,
                    public_enabled = s.public_enabled
                FROM tenancy.tenant_settings s
                WHERE s.tenant_id = t.id;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE tenancy.tenants
                    ALTER COLUMN time_zone DROP DEFAULT,
                    ALTER COLUMN default_locale DROP DEFAULT,
                    ALTER COLUMN currency_code DROP DEFAULT,
                    ALTER COLUMN public_enabled DROP DEFAULT;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_currency_code",
                schema: "tenancy",
                table: "tenants",
                sql: "currency_code ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenants_default_locale",
                schema: "tenancy",
                table: "tenants",
                sql: "default_locale ~ '^[a-z]{2}(-[A-Z]{2})?$'");

            migrationBuilder.DropTable(
                name: "tenant_settings",
                schema: "tenancy");
        }
    }
}
