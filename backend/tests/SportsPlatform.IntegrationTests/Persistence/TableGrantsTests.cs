using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

public sealed class TableGrantsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] _allPrivileges = ["SELECT", "INSERT", "UPDATE", "DELETE", "TRUNCATE", "REFERENCES", "TRIGGER"];

    [Fact]
    public async Task Every_table_has_a_declared_policy_and_the_matching_grants()
    {
        var actual = await ActualPrivilegesAsync();
        var historyTables = App.Services.GetServices<ModuleDatabase>()
            .Select(d => $"{d.Schema}.{ModuleDbContextOptions.MigrationsHistoryTable}")
            .ToHashSet();

        var problems = new List<string>();
        foreach (var table in actual.Keys.Select(key => key.Table).Distinct().Order())
        {
            TableAccess access;
            if (historyTables.Contains(table))
            {
                access = TableAccess.NoAccess;
            }
            else if (!TableAccessPolicies.Tables.TryGetValue(table, out access))
            {
                problems.Add($"{table}: no declared policy; add it to {nameof(TableAccessPolicies)}");
                continue;
            }

            foreach (var role in TableAccessPolicies.RuntimeRoles)
            {
                var expected = TableAccessPolicies.Exceptions.TryGetValue((table, role), out var exception)
                    ? exception.Privileges
                    : TableAccessPolicies.Privileges(access);
                var granted = actual[(table, role)];
                if (!granted.SequenceEqual(_allPrivileges.Where(expected.Contains)))
                {
                    problems.Add($"{table} ({access}): {role} has [{string.Join(", ", granted)}], expected [{string.Join(", ", expected)}]");
                }
            }
        }

        problems.AddRange(TableAccessPolicies.Tables.Keys
            .Where(table => !actual.ContainsKey((table, DatabaseRoles.App)))
            .Select(table => $"{table}: declared in {nameof(TableAccessPolicies)} but not in the database"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Append_only_entities_are_the_append_only_tables_and_have_the_triggers()
    {
        var declared = TableAccessPolicies.Tables.Where(t => t.Value == TableAccess.AppendOnly).Select(t => t.Key).Order().ToList();
        var migrator = Fixture.ConnectionString(DatabaseRoles.Migrator);
        var marked = App.Services.GetServices<ModuleDatabase>()
            .SelectMany(database =>
            {
                using var context = database.CreateContext(migrator);
                return context.Model.GetEntityTypes()
                    .Where(entityType => entityType.IsAppendOnly())
                    .Select(entityType => $"{entityType.GetSchema()}.{entityType.GetTableName()}")
                    .ToList();
            })
            .Order()
            .ToList();

        var triggers = await QueryAsync(
            DatabaseRoles.Migrator,
            "SELECT n.nspname || '.' || c.relname || ' ' || string_agg(t.tgname, ',' ORDER BY t.tgname) " +
            "FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace " +
            "WHERE NOT t.tgisinternal AND t.tgenabled = 'O' AND t.tgname LIKE 'append\\_only\\_%' " +
            "GROUP BY n.nspname, c.relname ORDER BY 1",
            reader => reader.GetString(0));

        marked.ShouldBe(declared);
        triggers.ShouldBe(declared.Select(table => $"{table} append_only_rows,append_only_truncate").ToList());
    }

    private async Task<Dictionary<(string Table, string Role), List<string>>> ActualPrivilegesAsync()
    {
        var rows = await QueryAsync(
            DatabaseRoles.Migrator,
            "SELECT n.nspname || '.' || c.relname, r.role, p.privilege, has_table_privilege(r.role, c.oid, p.privilege) " +
            "FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
            "CROSS JOIN unnest(@roles) AS r(role) CROSS JOIN unnest(@privileges) WITH ORDINALITY AS p(privilege, position) " +
            "WHERE c.relkind IN ('r', 'p') AND n.nspname NOT IN ('pg_catalog', 'information_schema') " +
            "AND n.nspname NOT LIKE 'pg\\_toast%' ORDER BY 1, 2, p.position",
            reader => (Table: reader.GetString(0), Role: reader.GetString(1), Privilege: reader.GetString(2), Granted: reader.GetBoolean(3)),
            new NpgsqlParameter("roles", TableAccessPolicies.RuntimeRoles),
            new NpgsqlParameter("privileges", _allPrivileges));

        return rows
            .GroupBy(row => (row.Table, row.Role))
            .ToDictionary(group => group.Key, group => group.Where(row => row.Granted).Select(row => row.Privilege).ToList());
    }
}
