using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// Every table with a <c>tenant_id</c> column, in every schema the migrations created (test schemas included), has
/// row-level security enabled and forced and the <c>tenant_isolation</c> policy (<c>EnableTenantRls</c>), and no other
/// permissive policy that would widen it. Exceptions are listed with a reason (S02 D4).
/// </summary>
public sealed class RlsCoverageTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    /// <summary>Tables (by name, in any schema) with a <c>tenant_id</c> column and no RLS, and why.</summary>
    private static readonly Dictionary<string, string> _exceptions = new()
    {
        ["outbox_messages"] =
            "the envelope's tenant, nullable for platform events; read across tenants by the outbox processor (Q15)",
    };

    private const string _tablesSql =
        $"""
        SELECT n.nspname || '.' || c.relname,
               c.relname,
               c.relrowsecurity,
               c.relforcerowsecurity,
               EXISTS (
                   SELECT 1 FROM pg_policies p
                   WHERE p.schemaname = n.nspname AND p.tablename = c.relname
                     AND p.policyname = '{RowLevelSecurityMigrationExtensions.PolicyName}'
                     AND p.permissive = 'PERMISSIVE' AND p.cmd = 'ALL' AND p.roles = ARRAY['public']::name[]
                     AND p.qual LIKE '%{TenantConnectionInterceptor.Setting}%' AND p.with_check = p.qual),
               (SELECT count(*) FROM pg_policies p
                WHERE p.schemaname = n.nspname AND p.tablename = c.relname AND p.permissive = 'PERMISSIVE')
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
        WHERE c.relkind IN ('r', 'p')
          AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
        ORDER BY 1
        """;

    [Fact]
    public async Task Every_table_with_a_tenant_column_has_the_tenant_isolation_policy()
    {
        var tables = await TablesAsync();

        tables.ShouldContain(t => t.Name == "audit.audit_entries");
        tables.ShouldContain(t => t.Name == "tenancy.tenant_status_history");
        tables.Where(t => !_exceptions.ContainsKey(t.Table))
            .Where(t => !t.Enabled || !t.Forced || !t.HasPolicy || t.PermissivePolicies != 1)
            .Select(t => $"{t.Name} (enabled: {t.Enabled}, forced: {t.Forced}, tenant_isolation: {t.HasPolicy}, " +
                $"permissive policies: {t.PermissivePolicies})")
            .ShouldBeEmpty("tables with tenant_id need EnableTenantRls in their migration, or an exception with a reason");
    }

    [Fact]
    public async Task Every_exception_still_names_a_table_with_a_tenant_column()
    {
        var tables = (await TablesAsync()).Select(t => t.Table).ToHashSet();

        _exceptions.Keys.Where(table => !tables.Contains(table)).ShouldBeEmpty("stale RLS exceptions");
    }

    private Task<List<TenantTable>> TablesAsync() => QueryAsync(
        DatabaseRoles.Migrator,
        _tablesSql,
        reader => new TenantTable(
            reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4),
            reader.GetInt64(5)));

    private sealed record TenantTable(string Name, string Table, bool Enabled, bool Forced, bool HasPolicy, long PermissivePolicies);
}
