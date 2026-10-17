using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

/// <summary>The grant helpers and the append-only trigger, exercised as each role.</summary>
public sealed class TableAccessTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _referenceTable = "conventions_test.reference_probes";
    private const string _appendOnlyTable = "conventions_test.append_only_probes";
    private const string _history = $"platform.{ModuleDbContextOptions.MigrationsHistoryTable}";

    public static TheoryData<string, string> RuntimeRoleChanges(string table) => new()
    {
        { DatabaseRoles.App, $"INSERT INTO {table} (id, code, created_at, created_by, updated_at, updated_by) VALUES (gen_random_uuid(), 'UG', now(), gen_random_uuid(), now(), gen_random_uuid())" },
        { DatabaseRoles.App, $"UPDATE {table} SET code = 'KE'" },
        { DatabaseRoles.App, $"DELETE FROM {table}" },
        { DatabaseRoles.Platform, $"INSERT INTO {table} (id, code, created_at, created_by, updated_at, updated_by) VALUES (gen_random_uuid(), 'UG', now(), gen_random_uuid(), now(), gen_random_uuid())" },
        { DatabaseRoles.Platform, $"UPDATE {table} SET code = 'KE'" },
        { DatabaseRoles.Platform, $"DELETE FROM {table}" },
    };

    public static TheoryData<string, string> AppendOnlyChanges => new()
    {
        { DatabaseRoles.App, $"UPDATE {_appendOnlyTable} SET note = 'changed'" },
        { DatabaseRoles.App, $"DELETE FROM {_appendOnlyTable}" },
        { DatabaseRoles.App, $"TRUNCATE {_appendOnlyTable}" },
        { DatabaseRoles.Platform, $"UPDATE {_appendOnlyTable} SET note = 'changed'" },
        { DatabaseRoles.Platform, $"DELETE FROM {_appendOnlyTable}" },
        { DatabaseRoles.Platform, $"TRUNCATE {_appendOnlyTable}" },
    };

    public static TheoryData<string> OwnerChanges =>
    [
        $"UPDATE {_appendOnlyTable} SET note = 'changed'",
        $"DELETE FROM {_appendOnlyTable}",
        $"TRUNCATE {_appendOnlyTable}",
    ];

    public static TheoryData<string> RuntimeRoles => [.. TableAccessPolicies.RuntimeRoles];

    [Theory]
    [MemberData(nameof(RuntimeRoleChanges), _referenceTable)]
    public async Task Runtime_roles_cannot_write_read_only_tables(string role, string sql)
    {
        await SeedReferenceProbeAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(role, sql));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Theory]
    [MemberData(nameof(RuntimeRoles))]
    public async Task Read_only_tables_are_seeded_by_the_migrator_and_read_by_runtime_roles(string role)
    {
        await SeedReferenceProbeAsync();

        var codes = await QueryAsync(role, $"SELECT code FROM {_referenceTable}", reader => reader.GetString(0));

        codes.ShouldBe(["UG"]);
    }

    [Theory]
    [MemberData(nameof(RuntimeRoles))]
    public async Task Runtime_roles_can_append_and_read(string role)
    {
        await AppendAsync(role, "Result submitted at Bukoto Community Stadium");

        var notes = await QueryAsync(role, $"SELECT note FROM {_appendOnlyTable}", reader => reader.GetString(0));

        notes.ShouldBe(["Result submitted at Bukoto Community Stadium"]);
    }

    [Theory]
    [MemberData(nameof(AppendOnlyChanges))]
    public async Task Runtime_roles_cannot_change_append_only_rows(string role, string sql)
    {
        await AppendAsync(DatabaseRoles.App, "Lineup approved");

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(role, sql));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Theory]
    [MemberData(nameof(OwnerChanges))]
    public async Task The_trigger_stops_the_owner_changing_append_only_rows(string sql)
    {
        await AppendAsync(DatabaseRoles.App, "Lineup approved");

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(DatabaseRoles.Migrator, sql));

        exception.SqlState.ShouldBe(TableAccessMigrationExtensions.AppendOnlyViolationSqlState);
        (await CountAsync(_appendOnlyTable)).ShouldBe(1);
    }

    [Fact]
    public async Task The_test_reset_empties_append_only_tables_and_leaves_the_trigger_on()
    {
        await AppendAsync(DatabaseRoles.App, "Lineup approved");

        await Fixture.ResetAsync();

        (await CountAsync(_appendOnlyTable)).ShouldBe(0);
        await AppendAsync(DatabaseRoles.App, "Result approved");
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync(DatabaseRoles.Migrator, $"DELETE FROM {_appendOnlyTable}"));
        exception.SqlState.ShouldBe(TableAccessMigrationExtensions.AppendOnlyViolationSqlState);
    }

    [Theory]
    [MemberData(nameof(RuntimeRoles))]
    public async Task Runtime_roles_cannot_read_the_migrations_history(string role)
    {
        var exception = await Should.ThrowAsync<PostgresException>(
            () => QueryAsync(role, $"SELECT count(*) FROM {_history}", reader => reader.GetInt64(0)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Migrating_again_keeps_the_migrations_history_closed()
    {
        await App.Services.GetRequiredService<MigrationRunner>().RunAsync(Ct);

        var granted = await QueryAsync(
            DatabaseRoles.Migrator,
            "SELECT bool_or(has_table_privilege(role, @table, 'SELECT, INSERT, UPDATE, DELETE')) FROM unnest(@roles) AS role",
            reader => reader.GetBoolean(0),
            new NpgsqlParameter("table", _history),
            new NpgsqlParameter("roles", TableAccessPolicies.RuntimeRoles));

        granted.ShouldBe([false]);
    }

    private Task<int> SeedReferenceProbeAsync() => ExecuteAsync(
        DatabaseRoles.Migrator,
        $"INSERT INTO {_referenceTable} (id, code, created_at, created_by, updated_at, updated_by) " +
        "VALUES (gen_random_uuid(), 'UG', now(), gen_random_uuid(), now(), gen_random_uuid())");

    private Task<int> AppendAsync(string role, string note) => ExecuteAsync(
        role,
        $"INSERT INTO {_appendOnlyTable} (id, note, created_at, created_by) " +
        $"VALUES (gen_random_uuid(), '{note}', now(), gen_random_uuid())");

    private async Task<long> CountAsync(string table) =>
        (await QueryAsync(DatabaseRoles.Migrator, $"SELECT count(*) FROM {table}", reader => reader.GetInt64(0))).Single();
}
