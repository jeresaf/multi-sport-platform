using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Tenancy.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Sports.EnableSport;

namespace SportsPlatform.Modules.Tenancy.Seed.Demo;

/// <summary>
/// The demo tenants' sports (S06 D23, S05 D19): each variant in <c>Seed/Demo/Data/demo-sports.json</c> is enabled through
/// <c>tenant.sport.enable</c> as the system actor. The variant codes are data, never C# (no sport code in generic code,
/// AR-11). A variant already enabled is skipped. A tenant that isn't <c>ACTIVE</c> is skipped too, as for the invitations,
/// but the run is still recorded: it is seeded when the seeder next runs (changed data or <c>seed --reset</c>). A
/// variant dropped from the file stays enabled (nothing is disabled).
/// </summary>
public sealed class DemoSportSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    private const string _file = "demo-sports.json";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    internal static IReadOnlyList<DemoTenantSports> Tenants { get; } =
        JsonSerializer.Deserialize<DemoSportsFile>(Read(), _options)?.Tenants
            ?? throw new InvalidOperationException($"Demo data file {_file} is empty.");

    public string Key => "tenancy.demo.sports";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>
    /// After the subscriptions (5), whose plan counts <c>MAX_ENABLED_SPORTS</c>; before the invitations (10) and the
    /// modules' demo data of a sport (<c>registry.demo.people</c>, 20).
    /// </summary>
    public int Order => 6;

    public string Checksum { get; } = $"v{_version}:{Convert.ToHexStringLower(SHA256.HashData(Read()))}";

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenants = context.Services.GetRequiredService<ITenantDirectory>();
        var inserted = 0;
        foreach (var demo in Tenants)
        {
            var found = await tenants.GetBySlugAsync(demo.Slug, cancellationToken);
            if (found.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Demo tenant '{demo.Slug}' doesn't exist: tenancy.demo.tenants runs first and creates it ({found.Error.Code}).");
            }

            if (found.Value.Status != TenantStatus.Active)
            {
                continue;
            }

            var tenantId = found.Value.Id;
            foreach (var variant in demo.Variants)
            {
                var enabled = await context.QueryAsTenantAsync(
                    tenantId, (services, ct) => services.GetRequiredService<IEnabledSports>().IsEnabledAsync(variant, ct), cancellationToken);
                if (enabled)
                {
                    continue;
                }

                await context.RunCommandAsync(
                    TenancyActions.TenantSportEnable,
                    tenantId,
                    async (services, ct) =>
                    {
                        DemoTenantSeeder.Succeed(
                            await services.GetRequiredService<EnableSportHandler>().HandleAsync(variant, ct), $"{demo.Slug} sport {variant}");
                        return true;
                    },
                    cancellationToken);
                inserted++;
            }
        }

        return new SeedOutcome(inserted, Updated: 0, Deactivated: 0);
    }

    private static byte[] Read()
    {
        using var stream = typeof(DemoSportSeeder).Assembly.GetManifestResourceStream($"SportsPlatform.Modules.Tenancy.Seed.Demo.Data.{_file}")
            ?? throw new InvalidOperationException($"Demo data file {_file} is not embedded.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

/// <summary>The shape of <c>demo-sports.json</c>.</summary>
internal sealed record DemoSportsFile(IReadOnlyList<DemoTenantSports> Tenants);

/// <summary>A demo tenant (by slug) and the variant codes it has enabled.</summary>
internal sealed record DemoTenantSports(string Slug, IReadOnlyList<string> Variants);
