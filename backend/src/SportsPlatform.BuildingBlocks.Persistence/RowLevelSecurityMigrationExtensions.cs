using Microsoft.EntityFrameworkCore.Migrations;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Row-level security for tenant-owned tables (docs/architecture/multi-tenancy.md, layer 2). Call
/// <see cref="EnableTenantRls"/> in the migration that creates a table with a <c>tenant_id</c> column.
/// </summary>
public static class RowLevelSecurityMigrationExtensions
{
    public const string PolicyName = "tenant_isolation";

    /// <summary>The policy's condition: the row's tenant is the connection's <c>app.tenant_id</c>; none matches no row.</summary>
    public const string TenantCondition =
        $"tenant_id = NULLIF(current_setting('{TenantConnectionInterceptor.Setting}', true), '')::uuid";

    /// <summary>
    /// Enables and forces RLS (the owner is bound too) and adds the <c>tenant_isolation</c> policy for reads
    /// (<c>USING</c>) and writes (<c>WITH CHECK</c>). <c>sp_platform</c> (BYPASSRLS) is not affected.
    /// </summary>
    public static MigrationBuilder EnableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var name = TableAccessMigrationExtensions.QualifiedName(schema, table);

        migrationBuilder.Sql($"ALTER TABLE {name} ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {name} FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            $"CREATE POLICY {PolicyName} ON {name} USING ({TenantCondition}) WITH CHECK ({TenantCondition});");
        return migrationBuilder;
    }

    /// <summary>The <c>Down</c> of <see cref="EnableTenantRls"/>, for a migration that keeps the table.</summary>
    public static MigrationBuilder DisableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        var name = TableAccessMigrationExtensions.QualifiedName(schema, table);

        migrationBuilder.Sql($"DROP POLICY IF EXISTS {PolicyName} ON {name};");
        migrationBuilder.Sql($"ALTER TABLE {name} NO FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {name} DISABLE ROW LEVEL SECURITY;");
        return migrationBuilder;
    }
}
