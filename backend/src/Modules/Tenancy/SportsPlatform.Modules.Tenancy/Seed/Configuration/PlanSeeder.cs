using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Seed.Configuration;

/// <summary>
/// The plans and their values (S04 D2), from <see cref="IPlanCatalogue"/>, as the owner: a new plan is inserted with its
/// deterministic ID (<c>tenancy.plan:&lt;code&gt;</c>), a changed name, rank or value updated, a plan no longer declared
/// deactivated (never deleted). Unchanged rows are not written at all.
/// </summary>
public sealed class PlanSeeder(IPlanCatalogue plans) : ISeeder
{
    public const string SeederKey = "tenancy.configuration.plans";

    /// <summary>Bump when the seeder writes something different from the same plans.</summary>
    private const int _version = 1;

    public string Key => SeederKey;

    public SeedCategory Category => SeedCategory.Configuration;

    public int Order => 0;

    public string Checksum => ComputeChecksum(plans.All);

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var db = context.CreateOwnerContext<TenancyDbContext>();
        var stored = await db.Set<Plan>().Include(p => p.Entitlements)
            .ToDictionaryAsync(p => p.Code, StringComparer.Ordinal, cancellationToken);
        var declared = plans.All.Select(plan => plan.Code).ToHashSet(StringComparer.Ordinal);

        int inserted = 0, updated = 0, deactivated = 0;
        foreach (var plan in stored.Values.Where(plan => !declared.Contains(plan.Code)))
        {
            deactivated += plan.Deactivate() ? 1 : 0;
        }

        foreach (var definition in plans.All)
        {
            if (stored.TryGetValue(definition.Code, out var plan))
            {
                updated += plan.Apply(definition) ? 1 : 0;
            }
            else
            {
                db.Add(new Plan(definition));
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SeedOutcome(inserted, updated, deactivated);
    }

    private static string ComputeChecksum(IReadOnlyList<PlanDefinition> plans)
    {
        var data = JsonSerializer.Serialize(new
        {
            version = _version,
            plans = plans.Select(plan => new
            {
                plan.Code,
                plan.Name,
                plan.Rank,
                values = plan.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary(),
            }),
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}
