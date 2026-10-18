using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Platform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "countries",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alpha3_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    numeric_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.id);
                    table.CheckConstraint("ck_countries_alpha3_code", "alpha3_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_countries_code", "code ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_countries_numeric_code", "numeric_code ~ '^[0-9]{3}$'");
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numeric_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    minor_units = table.Column<short>(type: "smallint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.id);
                    table.CheckConstraint("ck_currencies_code", "code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_currencies_minor_units", "minor_units BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_currencies_numeric_code", "numeric_code ~ '^[0-9]{3}$'");
                });

            migrationBuilder.CreateTable(
                name: "languages",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_languages", x => x.id);
                    table.CheckConstraint("ck_languages_code", "code ~ '^[a-z]{2}$'");
                });

            migrationBuilder.CreateTable(
                name: "time_zones",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_zones", x => x.id);
                    table.CheckConstraint("ck_time_zones_country_code", "country_code ~ '^[A-Z]{2}$'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_countries_alpha3_code",
                schema: "platform",
                table: "countries",
                column: "alpha3_code",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_countries_code",
                schema: "platform",
                table: "countries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_countries_numeric_code",
                schema: "platform",
                table: "countries",
                column: "numeric_code",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_currencies_code",
                schema: "platform",
                table: "currencies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currencies_numeric_code",
                schema: "platform",
                table: "currencies",
                column: "numeric_code",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_languages_code",
                schema: "platform",
                table: "languages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_zones_code",
                schema: "platform",
                table: "time_zones",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_zones_country_code",
                schema: "platform",
                table: "time_zones",
                column: "country_code");

            // Reference data is global and written only by the REFERENCE seeders as the owner.
            migrationBuilder.ReadOnlyForRuntimeRoles("platform", "countries");
            migrationBuilder.ReadOnlyForRuntimeRoles("platform", "currencies");
            migrationBuilder.ReadOnlyForRuntimeRoles("platform", "languages");
            migrationBuilder.ReadOnlyForRuntimeRoles("platform", "time_zones");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "countries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "currencies",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "languages",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "time_zones",
                schema: "platform");
        }
    }
}
