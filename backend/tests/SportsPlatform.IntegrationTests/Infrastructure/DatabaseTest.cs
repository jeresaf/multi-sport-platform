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

    public async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
