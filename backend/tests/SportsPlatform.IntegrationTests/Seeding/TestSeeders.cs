using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.IntegrationTests.Persistence;

namespace SportsPlatform.IntegrationTests.Seeding;

/// <summary>What the test seeders did, and how the next run should behave. Reset before each database test.</summary>
public sealed class SeedLog
{
    private readonly List<string> _runs = [];

    /// <summary>Keys of the seeders that ran, in order.</summary>
    public IReadOnlyList<string> Runs => _runs;

    /// <summary>The codes <see cref="ProbeReferenceSeeder"/> writes; its checksum follows them.</summary>
    public IReadOnlyList<string> ProbeCodes { get; set; } = ["UG", "KE"];

    /// <summary>A seeder that throws when it runs.</summary>
    public string? FailingKey { get; set; }

    /// <summary>Makes the DEMO seeder ask for an owner context.</summary>
    public bool DemoAsksForOwnerContext { get; set; }

    /// <summary>What the DEMO probe seeder does with its context, e.g. run a command.</summary>
    public Func<SeedContext, CancellationToken, Task>? DemoWork { get; set; }

    /// <summary>What the second REFERENCE seeder does with its context.</summary>
    public Func<SeedContext, CancellationToken, Task>? ReferenceWork { get; set; }

    public void Reset()
    {
        _runs.Clear();
        ProbeCodes = ["UG", "KE"];
        FailingKey = null;
        DemoAsksForOwnerContext = false;
        DemoWork = null;
        ReferenceWork = null;
    }

    internal void Ran(string key)
    {
        if (key == FailingKey)
        {
            throw new InvalidOperationException($"Seeder {key} failed (test).");
        }

        _runs.Add(key);
    }
}

internal static class TestSeederRegistration
{
    public static IServiceCollection AddTestSeeders(this IServiceCollection services, SeedLog log)
    {
        // Not reset here: hosts derived with WithWebHostBuilder register again, mid-test. DatabaseTest resets it.
        services.AddSingleton(log);
        // Out of run order on purpose: category, then Order, decide when a seeder runs, not registration.
        services.AddSeeder<TestProbeSeeder>();
        services.AddSeeder<DemoProbeSeeder>();
        services.AddSeeder<ConfigurationProbeSeeder>();
        services.AddSeeder<SecondReferenceSeeder>();
        services.AddSeeder<ProbeReferenceSeeder>();
        return services;
    }
}

/// <summary>Writes <c>conventions_test.reference_probes</c>, read-only for the runtime roles, so it must run as the owner.</summary>
internal sealed class ProbeReferenceSeeder(SeedLog log) : ISeeder
{
    public const string SeederKey = "test.reference.probes";

    public string Key => SeederKey;

    public SeedCategory Category => SeedCategory.Reference;

    public int Order => 0;

    public string Checksum => string.Join(",", log.ProbeCodes);

    public static Guid IdFor(string code) => DeterministicIds.Create($"test.reference_probe:{code}");

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        log.Ran(Key);
        var db = context.CreateOwnerContext<ConventionsTestDbContext>();
        var existing = await db.ReferenceProbes.Select(p => p.Code).ToListAsync(cancellationToken);
        var added = log.ProbeCodes.Except(existing).ToList();
        db.ReferenceProbes.AddRange(added.Select(code => new ReferenceProbe(IdFor(code), code)));
        await db.SaveChangesAsync(cancellationToken);
        return new SeedOutcome(added.Count, 0, 0);
    }
}

/// <summary>A seeder that only records that it ran.</summary>
internal abstract class RecordingSeeder(SeedLog log, string key, SeedCategory category, int order = 0) : ISeeder
{
    public string Key => key;

    public SeedCategory Category => category;

    public int Order => order;

    public string Checksum => "v1";

    protected SeedLog Log => log;

    public virtual Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        log.Ran(Key);
        return Task.FromResult(SeedOutcome.None);
    }
}

internal sealed class SecondReferenceSeeder(SeedLog log) : RecordingSeeder(log, "test.reference.second", SeedCategory.Reference, order: 1)
{
    public override async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        if (Log.ReferenceWork is { } work)
        {
            await work(context, cancellationToken);
        }

        return await base.RunAsync(context, cancellationToken);
    }
}

internal sealed class ConfigurationProbeSeeder(SeedLog log) : RecordingSeeder(log, "test.configuration.probe", SeedCategory.Configuration);

internal sealed class TestProbeSeeder(SeedLog log) : RecordingSeeder(log, "test.test.probe", SeedCategory.Test);

internal sealed class DemoProbeSeeder(SeedLog log) : RecordingSeeder(log, "test.demo.probe", SeedCategory.Demo)
{
    public override async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        if (Log.DemoAsksForOwnerContext)
        {
            context.CreateOwnerContext<ConventionsTestDbContext>();
        }

        if (Log.DemoWork is { } work)
        {
            await work(context, cancellationToken);
        }

        return await base.RunAsync(context, cancellationToken);
    }
}
