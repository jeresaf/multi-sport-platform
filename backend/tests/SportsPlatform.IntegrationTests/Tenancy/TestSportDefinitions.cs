using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// S05 D18: the sport quota can't be shown with football alone, so a test app lists three test-only variants beside the
/// seeded ones: two of one sport (<see cref="TestballFull"/>, <see cref="TestballShort"/>) and one of another
/// (<see cref="Testhoop"/>). Only the list is extended (what Tenancy reads); a test variant has no definition to get.
/// </summary>
internal sealed class TestSportDefinitions(ISportDefinitions seeded) : ISportDefinitions
{
    public const string TestballFull = "TESTBALL_FULL";
    public const string TestballShort = "TESTBALL_SHORT";
    public const string Testhoop = "TESTHOOP_FULL";

    private static readonly SportVariant[] _testVariants =
    [
        new(TestballFull, "TESTBALL", "Testball", "ADDITIVE", ContestShape.HeadToHead, 1),
        new(TestballShort, "TESTBALL", "Testball sevens", "ADDITIVE", ContestShape.HeadToHead, 1),
        new(Testhoop, "TESTHOOP", "Testhoop", "ADDITIVE", ContestShape.HeadToHead, 1),
    ];

    public async Task<IReadOnlyList<SportVariant>> ListAsync(CancellationToken cancellationToken) =>
        [.. await seeded.ListAsync(cancellationToken), .. _testVariants];

    public Task<SportDefinition?> GetAsync(string variant, int? version, CancellationToken cancellationToken) =>
        seeded.GetAsync(variant, version, cancellationToken);

    /// <summary>
    /// <paramref name="app"/> with these variants: the same settings, clock, authorization and test services, on a server
    /// of its own. Dispose it at the end of the test.
    /// </summary>
    public static WebApplicationFactory<Program> AppWithTestSports(TestApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var registered = services.Single(d => d.ServiceType == typeof(ISportDefinitions));
            var seededType = registered.ImplementationType
                ?? throw new InvalidOperationException("Sports Configuration registers ISportDefinitions by type.");
            services.Remove(registered);
            services.AddScoped(seededType);
            services.AddScoped<ISportDefinitions>(provider =>
                new TestSportDefinitions((ISportDefinitions)provider.GetRequiredService(seededType)));
        }));
    }
}
