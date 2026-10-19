using Npgsql;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>Tests that use the database share one container and run one at a time (each starts from a reset).</summary>
[CollectionDefinition(Name)]
public sealed class DatabaseTestsDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Database";
}

/// <summary>Base for database tests: resets the module tables before each test.</summary>
[Collection(DatabaseTestsDefinition.Name)]
public abstract class DatabaseTest(PostgresFixture fixture) : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; } = fixture;

    protected TestApp App => Fixture.App;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        App.Authorization.Reset();
        App.Seeds.Reset();
        await Fixture.ResetAsync();
    }

    /// <summary>Runs a query as one of the roles from <c>01-roles.sh</c>, outside the app.</summary>
    protected async Task<List<T>> QueryAsync<T>(
        string role, string sql, Func<NpgsqlDataReader, T> read, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString(role));
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

    /// <summary>Runs a statement as one of the roles from <c>01-roles.sh</c>, outside the app.</summary>
    protected async Task<int> ExecuteAsync(string role, string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString(role));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this); // CA1816; derived test classes have no finalizers.
        return ValueTask.CompletedTask;
    }
}
