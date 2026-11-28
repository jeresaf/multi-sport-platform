using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Seed.Demo;

/// <summary>
/// The demo tenants' venues (S07 D22, docs/product/pilot-customer.md), from <c>Seed/Demo/Data/demo-venues.json</c>
/// (data, not C#: it names sport variants, AR-11), each created through <c>venue.create</c> as the system actor. A
/// venue is found again by name key (the earliest created); one the tenant already has is left as it is. The data
/// expects no duplicate warning: a warning stops the run naming the candidates. A tenant that isn't <c>ACTIVE</c> is
/// skipped, as <c>registry.demo.people</c>. Before the organisations and teams, which name venues.
/// </summary>
public sealed class DemoVenueSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    private const string _file = "demo-venues.json";

    internal static IReadOnlyList<DemoTenantVenues> Tenants { get; } = DemoRegistryData.Load<DemoVenuesFile>(_file).Tenants;

    public string Key => "registry.demo.venues";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>After the tenants' sports (6) and people (20).</summary>
    public int Order => 30;

    public string Checksum { get; } = DemoRegistryData.Checksum(_version, _file);

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var inserted = 0;
        foreach (var demo in Tenants)
        {
            if (await DemoRegistryData.ActiveTenantAsync(context, demo.Slug, cancellationToken) is not { } tenantId)
            {
                continue;
            }

            foreach (var venue in demo.Venues)
            {
                if (await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, venue.Name, ct), cancellationToken) is not null)
                {
                    continue;
                }

                await context.RunCommandAsync(
                    RegistryActions.VenueCreate,
                    tenantId,
                    async (services, ct) =>
                    {
                        var request = venue.ToRequest();
                        await DemoRegistryData.ValidateAsync(services, request, $"demo venue {venue.Name}", ct);
                        var (_, refusal) = await services.GetRequiredService<CreateVenueHandler>().HandleAsync(request, ct);
                        DemoRegistryData.ThrowIfRefused(refusal, $"demo venue {venue.Name}");
                        return true;
                    },
                    cancellationToken);
                inserted++;
            }
        }

        return new SeedOutcome(inserted, Updated: 0, Deactivated: 0);
    }

    /// <summary>The ID of the tenant's venue of this name (the earliest created), or null.</summary>
    internal static async Task<Guid?> FindAsync(IServiceProvider services, string name, CancellationToken cancellationToken)
    {
        var key = Venue.NameKeyOf(name);

        // IDs are version 7: the earliest created comes first.
        return await services.GetRequiredService<RegistryDbContext>().Set<Venue>().AsNoTracking()
            .Where(v => v.NameKey == key)
            .OrderBy(v => v.Id)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>The shape of <c>demo-venues.json</c>; <c>_comment</c> says what the file is.</summary>
internal sealed record DemoVenuesFile(
    [property: JsonPropertyName("_comment")] string Comment,
    IReadOnlyList<DemoTenantVenues> Tenants);

internal sealed record DemoTenantVenues(string Slug, IReadOnlyList<DemoVenue> Venues);

/// <summary>A demo venue: <c>venue.create</c>'s fields, without coordinates (the places are invented).</summary>
internal sealed record DemoVenue(
    string Name,
    VenueType Type,
    string City,
    string TimeZone,
    string? ShortName = null,
    string? AddressLine = null,
    int? Capacity = null,
    IReadOnlyList<VenueSurface>? Surfaces = null,
    IReadOnlyList<string>? SupportedVariants = null)
{
    public CreateVenueRequest ToRequest() => new(
        Name,
        Type,
        City,
        TimeZone,
        ShortName: ShortName,
        AddressLine: AddressLine,
        Capacity: Capacity,
        Surfaces: Surfaces,
        SupportedVariants: SupportedVariants?.Cast<string?>().ToList());
}
