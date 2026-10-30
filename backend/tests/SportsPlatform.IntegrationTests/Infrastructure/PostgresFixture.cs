using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.SportsConfiguration;
using SportsPlatform.Modules.Tenancy;
using Testcontainers.PostgreSql;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container per test run, initialised exactly like docker compose: same image tag and the same
/// <c>01-roles.sh</c>. Migrations run as <c>sp_migrator</c>, the app runs as <c>sp_app</c>, and only the reset uses
/// the superuser — the app under test never gets the superuser connection.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
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

    private static readonly TimeSpan _startupTimeout = TimeSpan.FromMinutes(2);

    private PostgreSqlContainer? _container;
    private Respawner? _respawner;

    private static string RepositoryRoot => ComposeFile.RepositoryRoot;

    public static string ImageFromCompose { get; } = ComposeFile.Image("postgres");

    public TestApp App { get; private set; } = null!;

    /// <summary>The app's configuration: the container's role connections.</summary>
    public IReadOnlyDictionary<string, string?> AppSettings { get; private set; } = null!;

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
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(
                new UntilMigratorCanConnect(this),
                wait => wait.WithTimeout(_startupTimeout).WithInterval(TimeSpan.FromMilliseconds(500))))
            .Build();
        await StartContainerAsync();

        AppSettings = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ConnectionStringNames.App}"] = ConnectionString("sp_app"),
            [$"ConnectionStrings:{ConnectionStringNames.Migrator}"] = ConnectionString("sp_migrator"),
            [$"ConnectionStrings:{ConnectionStringNames.Platform}"] = ConnectionString("sp_platform"),
            // Few attempts keep the poison-message tests short.
            [$"{OutboxOptions.SectionName}:{nameof(OutboxOptions.MaxAttempts)}"] = "3",
        };
        App = new TestApp(AppSettings);
        await App.Services.GetRequiredService<MigrationRunner>().RunAsync();

        var databases = App.Services.GetServices<ModuleDatabase>().ToList();
        await using var connection = await OpenSuperuserConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = [.. ModelSchemas(databases)],
            TablesToIgnore =
            [
                .. databases.Select(d => new Table(d.Schema, ModuleDbContextOptions.MigrationsHistoryTable)),
                .. ReferenceTables.Select(table => new Table(PlatformModule.SchemaName, table)),
                .. PlanTables.Select(table => new Table(TenancyModule.SchemaName, table)),
                .. ConfigurationTables.Select(table => new Table(SportsConfigurationModule.SchemaName, table)),
            ],
        });

        // Real reference and configuration data once per run; the reset keeps it.
        await RestoreSeedDataAsync();
    }

    /// <summary>
    /// Platform's reference tables, seeded once when the fixture starts and kept by <see cref="ResetAsync"/>: the
    /// runtime roles can't change them, so a test that does (as the owner) restores them with
    /// <see cref="RestoreSeedDataAsync"/>.
    /// </summary>
    public static IReadOnlyList<string> ReferenceTables { get; } = ["countries", "currencies", "languages", "time_zones"];

    /// <summary>Tenancy's plan tables (CONFIGURATION, S04 D18): seeded and kept like <see cref="ReferenceTables"/>.</summary>
    public static IReadOnlyList<string> PlanTables { get; } = ["plans", "plan_entitlements"];

    /// <summary>Sports Configuration's tables (CONFIGURATION, S05 D4): seeded and kept like <see cref="ReferenceTables"/>.</summary>
    public static IReadOnlyList<string> ConfigurationTables { get; } = ["sport_definitions"];

    /// <summary>
    /// Resets, then seeds the real reference and configuration data again (the reset empties <c>seed_runs</c>, so every
    /// REFERENCE and CONFIGURATION seeder runs): after a test changed that data or dropped the schemas.
    /// </summary>
    public async Task RestoreSeedDataAsync()
    {
        await ResetAsync();
        var report = await App.Services.GetRequiredService<SeedRunner>()
            .RunAsync(new SeedRequest(SeedCategory.Configuration), TestContext.Current.CancellationToken);
        if (report.IsRefused)
        {
            throw new InvalidOperationException($"Reference and configuration data not restored: {report.Refusal}");
        }

        await ResetAsync();
    }

    /// <summary>Each module's schema and any other schema its model maps to (Platform's <c>audit</c>).</summary>
    private string[] ModelSchemas(IEnumerable<ModuleDatabase> databases)
    {
        using var scope = App.Services.CreateScope();
        return [.. databases
            .SelectMany(database => ((DbContext)scope.ServiceProvider.GetRequiredService(database.ContextType)).Model
                .GetEntityTypes().Select(entity => entity.GetSchema()).Append(database.Schema))
            .OfType<string>()
            .Distinct()];
    }

    // A broken init script would otherwise hang until the CI job times out, with no useful output.
    private async Task StartContainerAsync()
    {
        try
        {
            await _container!.StartAsync();
        }
        catch (TimeoutException exception)
        {
            var (stdout, stderr) = await _container!.GetLogsAsync();
            throw new InvalidOperationException(
                $"sp_migrator could not log in within {_startupTimeout}; check 01-roles.sh.\n" +
                $"Container stdout:\n{stdout}\nContainer stderr:\n{stderr}",
                exception);
        }
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

    /// <summary>
    /// Empties every module table (not the migrations history) so each test starts clean. Respawn truncates, which
    /// the append-only triggers reject; as superuser with <c>session_replication_role = replica</c> ordinary
    /// triggers don't fire. The setting dies with the connection, which is never pooled.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = await OpenSuperuserConnectionAsync();
        await using (var command = new NpgsqlCommand("SET session_replication_role = replica", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

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
        var connection = new NpgsqlConnection(BuildConnectionString("postgres") + ";Pooling=false");
        await connection.OpenAsync();
        return connection;
    }

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
