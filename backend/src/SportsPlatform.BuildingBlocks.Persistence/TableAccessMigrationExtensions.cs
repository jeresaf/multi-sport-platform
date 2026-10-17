using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Narrows the default grants from <c>01-roles.sh</c> (DML on every table for <c>sp_app</c> and <c>sp_platform</c>).
/// Call one of these in the migration that creates the table; tables left read-write need no call but must still
/// be declared in the grants test.
/// </summary>
public static partial class TableAccessMigrationExtensions
{
    /// <summary>The SQLSTATE raised by the append-only trigger (PL/pgSQL's <c>raise_exception</c>).</summary>
    public const string AppendOnlyViolationSqlState = "P0001";

    /// <summary>Reference and global tables: the runtime roles may only read. Seeds for them run as <c>sp_migrator</c>.</summary>
    public static MigrationBuilder ReadOnlyForRuntimeRoles(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var name = QualifiedName(schema, table);

        migrationBuilder.Sql($"REVOKE INSERT, UPDATE, DELETE ON {name} FROM {DatabaseRoles.RuntimeRoles};");
        return migrationBuilder;
    }

    /// <summary>
    /// Audit and timeline tables (AR-19): the runtime roles may read and insert only, and triggers reject UPDATE,
    /// DELETE and TRUNCATE for everyone, including the owner. Only a superuser with
    /// <c>session_replication_role = replica</c> (the test reset) or a reviewed DDL migration by the owner gets past
    /// them. A <c>Down</c> that drops the table leaves the schema's function in place (harmless; re-created with
    /// <c>CREATE OR REPLACE</c>). On a partitioned table, apply it to each partition as well.
    /// </summary>
    public static MigrationBuilder AppendOnly(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var name = QualifiedName(schema, table);
        var function = $"\"{schema}\".reject_append_only_change";

        migrationBuilder.Sql($"REVOKE UPDATE, DELETE ON {name} FROM {DatabaseRoles.RuntimeRoles};");

        // One function per schema, so a module's migrations never depend on another module's.
        migrationBuilder.Sql(
            $$"""
            CREATE OR REPLACE FUNCTION {{function}}() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
              RAISE EXCEPTION '%.% is append-only: % is not allowed', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP
                USING ERRCODE = '{{AppendOnlyViolationSqlState}}';
            END
            $fn$;
            """);
        migrationBuilder.Sql(
            $"CREATE TRIGGER append_only_rows BEFORE UPDATE OR DELETE ON {name} FOR EACH ROW EXECUTE FUNCTION {function}();");
        migrationBuilder.Sql(
            $"CREATE TRIGGER append_only_truncate BEFORE TRUNCATE ON {name} FOR EACH STATEMENT EXECUTE FUNCTION {function}();");
        return migrationBuilder;
    }

    internal static string QualifiedName(string schema, string table) => $"{Identifier(schema)}.{Identifier(table)}";

    private static string Identifier(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return SnakeCaseIdentifier().IsMatch(name)
            ? $"\"{name}\""
            : throw new ArgumentException($"'{name}' is not a snake_case identifier.", nameof(name));
    }

    // \z, not $: $ also matches before a trailing newline. 63 bytes is PostgreSQL's identifier limit.
    [GeneratedRegex(@"^[a-z_][a-z0-9_]{0,62}\z")]
    private static partial Regex SnakeCaseIdentifier();
}
