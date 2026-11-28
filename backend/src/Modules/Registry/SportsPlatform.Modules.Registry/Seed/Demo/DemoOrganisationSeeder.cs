using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;
using SportsPlatform.Modules.Registry.Features.Organisations.Logo;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Seed.Demo;

/// <summary>
/// The demo tenants' organisations (S07 D22, docs/product/pilot-customer.md), from
/// <c>Seed/Demo/Data/demo-organisations.json</c>: KMSA's clubs and ELSSC's schools, each created through
/// <c>organisation.create</c> as the system actor with its primary venue (named in the file, seeded by
/// <c>registry.demo.venues</c>), then given a generated placeholder logo (<c>organisation.logo.set</c>), never a real
/// club's. An organisation is found again by its display name's key (the earliest created); a run only adds the logo
/// one lacks. The data expects no duplicate warning: a warning stops the run naming the candidates. A tenant that
/// isn't <c>ACTIVE</c> is skipped. Before the teams, which name organisations.
/// </summary>
public sealed class DemoOrganisationSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    private const string _file = "demo-organisations.json";

    private const int _logoSize = 256;

    internal static IReadOnlyList<DemoTenantOrganisations> Tenants { get; } = DemoRegistryData.Load<DemoOrganisationsFile>(_file).Tenants;

    public string Key => "registry.demo.organisations";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>After the venues (30).</summary>
    public int Order => 31;

    public string Checksum { get; } = DemoRegistryData.Checksum(_version, _file);

    public async Task<SeedOutcome> RunAsync(SeedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int inserted = 0, updated = 0;
        foreach (var demo in Tenants)
        {
            if (await DemoRegistryData.ActiveTenantAsync(context, demo.Slug, cancellationToken) is not { } tenantId)
            {
                continue;
            }

            for (var index = 0; index < demo.Organisations.Count; index++)
            {
                var organisation = demo.Organisations[index];
                var what = $"demo organisation {organisation.DisplayName}";
                var state = await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, organisation.DisplayName, ct), cancellationToken);
                var created = state is null;
                if (state is null)
                {
                    await context.RunCommandAsync(
                        RegistryActions.OrganisationCreate,
                        tenantId,
                        async (services, ct) =>
                        {
                            var venueId = organisation.PrimaryVenue is { } venue
                                ? await DemoVenueSeeder.FindAsync(services, venue, ct)
                                    ?? throw new InvalidOperationException($"The primary venue of {what}, {venue}, isn't seeded: list it in demo-venues.json.")
                                : (Guid?)null;
                            var request = organisation.ToRequest(venueId);
                            await DemoRegistryData.ValidateAsync(services, request, what, ct);
                            var (_, refusal) = await services.GetRequiredService<CreateOrganisationHandler>().HandleAsync(request, ct);
                            DemoRegistryData.ThrowIfRefused(refusal, what);
                            return true;
                        },
                        cancellationToken);
                    state = await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, organisation.DisplayName, ct), cancellationToken)
                        ?? throw new InvalidOperationException($"The {what} wasn't found after create.");
                }

                var changed = false;
                if (!state.HasLogo)
                {
                    var id = state.Id;
                    await context.RunCommandAsync(
                        RegistryActions.OrganisationLogoSet,
                        tenantId,
                        async (services, ct) =>
                        {
                            using var file = new MemoryStream(PlaceholderPng.Logo(_logoSize, index));
                            var refusal = await services.GetRequiredService<SetOrganisationLogoHandler>().HandleAsync(id, file, IfMatch.None, ct);
                            DemoRegistryData.ThrowIfRefused(refusal, $"the logo of {what}");
                            return true;
                        },
                        cancellationToken);
                    changed = true;
                }

                inserted += created ? 1 : 0;
                updated += !created && changed ? 1 : 0;
            }
        }

        return new SeedOutcome(inserted, updated, Deactivated: 0);
    }

    /// <summary>The ID of the tenant's organisation of this display name (the earliest created), or null.</summary>
    internal static async Task<Guid?> FindIdAsync(IServiceProvider services, string displayName, CancellationToken cancellationToken) =>
        (await FindAsync(services, displayName, cancellationToken))?.Id;

    private static async Task<OrganisationState?> FindAsync(IServiceProvider services, string displayName, CancellationToken cancellationToken)
    {
        var key = Organisation.NameKeyOf(displayName);

        // IDs are version 7: the earliest created comes first.
        return await services.GetRequiredService<RegistryDbContext>().Set<Organisation>().AsNoTracking()
            .Where(o => o.DisplayNameKey == key)
            .OrderBy(o => o.Id)
            .Select(o => new OrganisationState(o.Id, o.LogoAssetId != null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record OrganisationState(Guid Id, bool HasLogo);
}

/// <summary>The shape of <c>demo-organisations.json</c>; <c>_comment</c> says what the file is.</summary>
internal sealed record DemoOrganisationsFile(
    [property: JsonPropertyName("_comment")] string Comment,
    IReadOnlyList<DemoTenantOrganisations> Tenants);

internal sealed record DemoTenantOrganisations(string Slug, IReadOnlyList<DemoOrganisation> Organisations);

/// <summary>A demo organisation: <c>organisation.create</c>'s fields, with the primary venue by name.</summary>
internal sealed record DemoOrganisation(
    string DisplayName,
    OrganisationType Type,
    string? LegalName = null,
    string? ShortName = null,
    string? City = null,
    int? FoundedYear = null,
    string? PrimaryVenue = null)
{
    public CreateOrganisationRequest ToRequest(Guid? primaryVenueId) => new(
        Type,
        DisplayName,
        LegalName: LegalName,
        ShortName: ShortName,
        City: City,
        FoundedYear: FoundedYear,
        PrimaryVenueId: primaryVenueId);
}
