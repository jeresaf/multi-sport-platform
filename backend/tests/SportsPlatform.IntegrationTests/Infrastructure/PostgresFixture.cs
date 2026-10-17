using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using SportsPlatform.BuildingBlocks.Persistence;
using Testcontainers.PostgreSql;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container per test run, initialised exactly like docker compose: same image tag and the same
/// <c>01-roles.sh</c>. Migrations run as <c>sp_migrator</c>, the app runs as <c>sp_app</c>, and only the reset uses
/// the superuser — the app under test never gets the superuser connection.
/// </summary>
public sealed partial class PostgresFixture : IAsyncLifetime
{
    public const string Database = "sportsplatform";

    // Test-only passwords for a throwaway container.
    private static readonly Dictionary<string, string> _passwords = new()
    {
        ["postgres"] = "postgres-test-only",
        ["sp_migrator"] = "sp-migrator-test-only",
        ["sp_app"] = "sp-app-test-only",
        ["sp_platform"] = "sp-platform-test-only",
    };

    private PostgreSqlContainer? _container;
    private Respawner? _respawner;

    // Declared before ImageFromCompose: static initialisers run in declaration order.
    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string ImageFromCompose { get; } = ReadComposeImage();

    public TestApp App { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(ImageFromCompose)
            .WithUsername("postgres")
            .WithPassword(_passwords["postgres"])
            .WithDatabase("postgres")
            .WithEnvironment("SP_DATABASE", Database)
            .WithEnvironment("SP_MIGRATOR_PASSWORD", _passwords["sp_migrator"])
            .WithEnvironment("SP_APP_PASSWORD", _passwords["sp_app"])
            .WithEnvironment("SP_PLATFORM_PASSWORD", _passwords["sp_platform"])
            .WithResourceMapping(
                await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot, "infrastructure", "local", "postgres", "init", "01-roles.sh")),
                "/docker-entrypoint-initdb.d/01-roles.sh",
                Convert.ToUInt32("755", 8))
            // The init scripts run on a socket-only server that restarts afterwards, so "server answers" is not
            // enough: wait until sp_migrator can log in over TCP.
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new UntilMigratorCanConnect(this)))
            .Build();
        await _container.StartAsync();

        App = new TestApp(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ConnectionStringNames.App}"] = ConnectionString("sp_app"),
            [$"ConnectionStrings:{ConnectionStringNames.Migrator}"] = ConnectionString("sp_migrator"),
            [$"ConnectionStrings:{ConnectionStringNames.Platform}"] = ConnectionString("sp_platform"),
        });
        await App.Services.GetRequiredService<MigrationRunner>().RunAsync();

        var databases = App.Services.GetServices<ModuleDatabase>().ToList();
        await using var connection = await OpenSuperuserConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = [.. databases.Select(d => d.Schema)],
            TablesToIgnore = [.. databases.Select(d => new Table(d.Schema, ModuleDbContextOptions.MigrationsHistoryTable))],
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Empties every module table (not the migrations history) so each test starts clean.</summary>
    public async Task ResetAsync()
    {
        await using var connection = await OpenSuperuserConnectionAsync();
        await _respawner!.ResetAsync(connection);
    }

    /// <summary>A connection string for one of the roles from <c>01-roles.sh</c> (not the superuser).</summary>
    public string ConnectionString(string role)
    {
        if (role == "postgres")
        {
            throw new ArgumentException("Tests use the superuser only through ResetAsync.", nameof(role));
        }

        return BuildConnectionString(role);
    }

    private string BuildConnectionString(string role) => new NpgsqlConnectionStringBuilder
    {
        Host = _container!.Hostname,
        Port = _container.GetMappedPublicPort(PostgreSqlBuilder.PostgreSqlPort),
        Database = Database,
        Username = role,
        Password = _passwords[role],
    }.ConnectionString;

    private async Task<NpgsqlConnection> OpenSuperuserConnectionAsync()
    {
        var connection = new NpgsqlConnection(BuildConnectionString("postgres"));
        await connection.OpenAsync();
        return connection;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("docker-compose.yml not found above the test output folder.");
    }

    private static string ReadComposeImage()
    {
        var compose = File.ReadAllText(Path.Combine(RepositoryRoot, "docker-compose.yml"));
        var match = ComposePostgresImage().Match(compose);
        return match.Success
            ? match.Groups["image"].Value
            : throw new InvalidOperationException("No postgres image found in docker-compose.yml.");
    }

    [GeneratedRegex(@"^\s*image:\s*(?<image>postgres:\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex ComposePostgresImage();

    private sealed class UntilMigratorCanConnect(PostgresFixture fixture) : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            try
            {
                await using var connection = new NpgsqlConnection(fixture.BuildConnectionString("sp_migrator"));
                await connection.OpenAsync();
                return true;
            }
            catch (NpgsqlException)
            {
                return false;
            }
        }
    }
}
