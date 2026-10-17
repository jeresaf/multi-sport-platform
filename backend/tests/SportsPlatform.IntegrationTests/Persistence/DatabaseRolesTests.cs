using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.IntegrationTests.Persistence;

public sealed class DatabaseRolesTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_roles_from_01_roles_sh_exist_with_their_attributes()
    {
        var roles = await QueryAsync(
            "SELECT rolname, rolsuper, rolbypassrls, rolcreatedb, rolcreaterole FROM pg_roles " +
            "WHERE rolname LIKE 'sp\\_%' ORDER BY rolname",
            reader => $"{reader.GetString(0)} super={reader.GetBoolean(1)} bypassrls={reader.GetBoolean(2)} " +
                $"createdb={reader.GetBoolean(3)} createrole={reader.GetBoolean(4)}");

        roles.ShouldBe(
        [
            "sp_app super=False bypassrls=False createdb=False createrole=False",
            "sp_migrator super=False bypassrls=False createdb=False createrole=False",
            "sp_platform super=False bypassrls=True createdb=False createrole=False",
        ]);
    }

    [Fact]
    public async Task The_server_runs_the_docker_compose_image()
    {
        var major = PostgresFixture.ImageFromCompose.Split(':')[1].Split('.')[0];

        var version = await QueryAsync("SHOW server_version", reader => reader.GetString(0));

        version.Single().ShouldStartWith(major + ".");
    }

    [Fact]
    public async Task The_app_connects_as_sp_app()
    {
        using var scope = App.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var user = await context.Database.SqlQueryRaw<string>("SELECT current_user::text AS \"Value\"").SingleAsync(Ct);

        user.ShouldBe("sp_app");
    }

    [Fact]
    public async Task Module_schemas_and_tables_are_owned_by_sp_migrator()
    {
        var schemas = App.Services.GetServices<ModuleDatabase>().Select(d => d.Schema).ToArray();

        var owners = await QueryAsync(
            "SELECT 'schema ' || nspname || ' ' || pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = ANY(@schemas) " +
            "UNION ALL SELECT 'table ' || schemaname || '.' || tablename || ' ' || tableowner FROM pg_tables " +
            "WHERE schemaname = ANY(@schemas)",
            reader => reader.GetString(0),
            new NpgsqlParameter("schemas", schemas));

        owners.ShouldContain($"schema platform sp_migrator");
        owners.ShouldContain($"table platform.{ModuleDbContextOptions.MigrationsHistoryTable} sp_migrator");
        owners.ShouldContain($"table {ConventionsTestDbContext.SchemaName}.convention_probes sp_migrator");
        owners.ShouldAllBe(owner => owner.EndsWith(" sp_migrator", StringComparison.Ordinal));
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> read, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString("sp_migrator"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(Ct);

        var rows = new List<T>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(read(reader));
        }

        return rows;
    }
}
