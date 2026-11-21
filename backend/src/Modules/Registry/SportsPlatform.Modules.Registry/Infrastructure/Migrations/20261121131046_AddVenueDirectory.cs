using Microsoft.EntityFrameworkCore.Migrations;
using SportsPlatform.BuildingBlocks.Persistence;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <summary>
    /// S07-T1b-2: <c>registry.venues.sort_key</c>, the directory's sort key (the normalised name, word order kept,
    /// collation "C"), with its paging index. Existing venues get the key from SQL that does what
    /// <c>NameNormaliser.Normalise</c> does for Latin script (NFD, accents and the Latin modifier letters such as ʼ dropped,
    /// lower case, every character but a letter, a digit or whitespace dropped, whitespace (every space .NET counts,
    /// the no-break ones included) collapsed; it assumes a UTF-8 database with a non-C ctype, as PostgreSQL's image and
    /// managed defaults). Modifier letters of other scripts stay (review #119 R1); a name SQL can't normalise (another script, outside
    /// the database's character classes) falls back to its name key. The next update of the venue writes the key from C#.
    /// </summary>
    public partial class AddVenueDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sort_key",
                schema: "registry",
                table: "venues",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                collation: "C");

            // Forced RLS hides every row from the migrator, the table's owner: the helper lifts FORCE for the update.
            migrationBuilder.UpdateTenantRowsAsOwner("registry", "venues", """
                UPDATE registry.venues
                SET sort_key = COALESCE(NULLIF(normalize(
                    btrim(regexp_replace(
                        regexp_replace(
                            regexp_replace(lower(normalize(name, NFD)), '[\u02b0-\u02ff\u1d2c-\u1d6a\u1d78\u1d9b-\u1dbf\u2071\u207f\u2090-\u209c\u2c7c\u2c7d\ua7f8\ua7f9\uab5c-\uab5f\u0300-\u036f]', '', 'g'),
                            '[^[:alnum:][:space:]\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]', '', 'g'),
                        '[[:space:]\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+', ' ', 'g')),
                    NFC), ''), name_key);
                """);
            migrationBuilder.Sql("ALTER TABLE registry.venues ALTER COLUMN sort_key DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_venues_tenant_id_sort_key_id",
                schema: "registry",
                table: "venues",
                columns: new[] { "tenant_id", "sort_key", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_venues_sort_key",
                schema: "registry",
                table: "venues",
                sql: "sort_key <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_venues_tenant_id_sort_key_id",
                schema: "registry",
                table: "venues");

            migrationBuilder.DropCheckConstraint(
                name: "ck_venues_sort_key",
                schema: "registry",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "sort_key",
                schema: "registry",
                table: "venues");
        }
    }
}
