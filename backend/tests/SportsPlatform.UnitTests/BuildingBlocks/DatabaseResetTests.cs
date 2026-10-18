using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;

namespace SportsPlatform.UnitTests.BuildingBlocks;

/// <summary>The reset's own checks run before it connects, so they need no database.</summary>
public sealed class DatabaseResetTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    [InlineData("localhost:5432")]
    [InlineData("::1")]
    [InlineData("[::1]:5432")]
    [InlineData("/var/run/postgresql")]
    [InlineData("postgres")]
    [InlineData("localhost,127.0.0.1:5433")]
    public void A_local_database_may_be_reset_in_Development(string host)
    {
        Reset(host).Refusal().ShouldBeNull();
    }

    [Theory]
    [InlineData("kmsa-staging.postgres.database.azure.com")]
    [InlineData("10.20.0.5:5432")]
    [InlineData("localhost,kmsa-staging.postgres.database.azure.com")]
    [InlineData("localhost.kmsa.example")]
    [InlineData("")]
    public void Any_other_database_is_refused_and_named(string host)
    {
        var refusal = Reset(host).Refusal();

        refusal.ShouldNotBeNull();
        refusal.ShouldContain(DatabaseReset.AllowResetOfKey);
    }

    [Fact]
    public void The_refusal_names_the_remote_host()
    {
        Reset("localhost,kmsa-staging.postgres.database.azure.com:5432").Refusal()
            .ShouldNotBeNull().ShouldContain("'kmsa-staging.postgres.database.azure.com'");
    }

    [Fact]
    public void A_named_non_local_Development_database_may_be_reset()
    {
        Reset("dev-db.kmsa.example", allowResetOf: "dev-db.kmsa.example").Refusal().ShouldBeNull();
        Reset("other-db.kmsa.example", allowResetOf: "dev-db.kmsa.example").Refusal().ShouldNotBeNull();
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("Testing")]
    public void Outside_Development_even_a_local_database_is_refused(string environment)
    {
        Reset("localhost", environment: environment).Refusal().ShouldNotBeNull().ShouldContain("only in Development");
    }

    private static DatabaseReset Reset(string host, string environment = "Development", string? allowResetOf = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ConnectionStringNames.Migrator}"] = $"Host={host};Database=sportsplatform;Username=sp_migrator",
            [DatabaseReset.AllowResetOfKey] = allowResetOf,
        }).Build();

        // Refusal never touches the databases or the migration runner.
        return new DatabaseReset([], null!, configuration, new SeedEnvironment(environment), NullLogger<DatabaseReset>.Instance);
    }
}
