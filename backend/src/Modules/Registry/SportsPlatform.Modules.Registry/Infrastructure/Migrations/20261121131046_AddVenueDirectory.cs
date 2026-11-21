using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPlatform.Modules.Registry.Infrastructure.Migrations
{
    /// <summary>
    /// S07-T1b-2: <c>registry.venues.sort_key</c>, the directory's sort key (the normalised name, word order kept,
    /// collation "C"), with its paging index. Existing venues get the key from SQL that does what
    /// <c>NameNormaliser.Normalise</c> does for Latin script (NFD, accents dropped, lower case, every character but a
    /// letter, a digit or whitespace dropped, whitespace collapsed); a name SQL can't normalise (another script, outside
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

            // The migrator owns the table, but forced RLS hides every row from it without a tenant: lift FORCE for the
            // update only, in the migration's transaction.
            migrationBuilder.Sql("""
                ALTER TABLE registry.venues NO FORCE ROW LEVEL SECURITY;
                UPDATE registry.venues
                SET sort_key = COALESCE(NULLIF(normalize(
                    btrim(regexp_replace(
                        regexp_replace(
                            regexp_replace(lower(normalize(name, NFD)), '[\u0300-\u036f]', '', 'g'),
                            '[^[:alnum:][:space:]]', '', 'g'),
                        '[[:space:]]+', ' ', 'g')),
                    NFC), ''), name_key);
                ALTER TABLE registry.venues FORCE ROW LEVEL SECURITY;
                """);

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
