using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>
/// S05 D18: the sport quota can't be shown with football alone, so a test app lists three test-only variants beside the
/// seeded ones: two of one sport (<see cref="TestballFull"/>, <see cref="TestballShort"/>) and one of another
/// (<see cref="Testhoop"/>). Each has a definition made from the seeded football one with test positions of its own
/// (S06-T4: profiles check their codes against the definition), so tests never depend on a real sport's codes.
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

    /// <summary>The positions of the two Testball variants.</summary>
    public static IReadOnlyList<string> TestballPositions { get; } = ["TB_KEEPER", "TB_BACK", "TB_MID", "TB_WING", "TB_STRIKER"];

    /// <summary>The positions of Testhoop.</summary>
    public static IReadOnlyList<string> TesthoopPositions { get; } = ["TH_GUARD", "TH_FORWARD", "TH_CENTRE"];

    public async Task<SportDefinition?> GetAsync(string variant, int? version, CancellationToken cancellationToken)
    {
        if (_testVariants.SingleOrDefault(test => test.Variant == variant) is not { } test)
        {
            return await seeded.GetAsync(variant, version, cancellationToken);
        }

        if (version is not (null or 1))
        {
            return null;
        }

        var template = await seeded.GetAsync(TenantSportRequests.Football, version: null, cancellationToken)
            ?? throw new InvalidOperationException("The seeded football definition is missing.");
        var positions = test.Sport == "TESTHOOP" ? TesthoopPositions : TestballPositions;
        return template with
        {
            Variant = test.Variant,
            Sport = test.Sport,
            Version = 1,
            Name = test.Name,
            Positions = [.. positions.Select(code => new PositionDefinition(code, $"Position {code}", "TEST"))],
        };
    }

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
