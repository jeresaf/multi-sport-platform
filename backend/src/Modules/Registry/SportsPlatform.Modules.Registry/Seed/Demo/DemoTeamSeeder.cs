using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.Teams.ChangeTeamStatus;
using SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;
using SportsPlatform.Modules.Registry.Features.Teams.Images;
using SportsPlatform.Modules.Registry.Features.Teams.RenameTeam;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Seed.Demo;

/// <summary>
/// The demo tenants' teams (S07 D22, docs/product/pilot-customer.md), from <c>Seed/Demo/Data/demo-teams.json</c>
/// (data, not C#: it names the sport variant, AR-11): football only until S15–S16 publish rugby and basketball. Each
/// club of the file lists its teams by category; every change goes through the Registry's commands as the system
/// actor: <c>team.create</c> under the club's organisation with its home venue (both by name, seeded earlier), a rename
/// from the club's <c>nameSince</c> when the team was created under its first name, a generated placeholder badge, and
/// archiving last (an archived team takes no new name or image).
/// <para>
/// A team is found again by variant, category and a current or former name of the club (the earliest created), so a
/// team renamed since (the slice demo renames Bunga Breakers FC) is still found; each step runs only when what it adds
/// is missing. The data expects no duplicate warning: a warning stops the run naming the candidates. A tenant that
/// isn't <c>ACTIVE</c> is skipped. After the organisations (31) and venues (30).
/// </para>
/// </summary>
public sealed class DemoTeamSeeder : ISeeder
{
    /// <summary>Bump when the seeder writes something different from the same data.</summary>
    private const int _version = 1;

    private const string _file = "demo-teams.json";

    private const int _badgeSize = 256;

    internal static IReadOnlyList<DemoTenantTeams> Tenants { get; } = Load();

    public string Key => "registry.demo.teams";

    public SeedCategory Category => SeedCategory.Demo;

    /// <summary>After the organisations (31).</summary>
    public int Order => 32;

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

            for (var clubIndex = 0; clubIndex < demo.Clubs.Count; clubIndex++)
            {
                var club = demo.Clubs[clubIndex];
                foreach (var team in club.Teams)
                {
                    var what = $"demo team {club.Name} ({team.GenderCategory} {team.AgeGroup} {team.Level ?? TeamLevel.First})";
                    var state = await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, demo.Variant, club, team, ct), cancellationToken);
                    var created = state is null;
                    if (state is null)
                    {
                        await context.RunCommandAsync(
                            RegistryActions.TeamCreate,
                            tenantId,
                            async (services, ct) =>
                            {
                                var request = await CreateRequestAsync(services, demo.Variant, club, team, what, ct);
                                await DemoRegistryData.ValidateAsync(services, request, what, ct);
                                var (_, refusal) = await services.GetRequiredService<CreateTeamHandler>().HandleAsync(request, ct);
                                DemoRegistryData.ThrowIfRefused(refusal, what);
                                return true;
                            },
                            cancellationToken);
                        state = await context.QueryAsTenantAsync(tenantId, (services, ct) => FindAsync(services, demo.Variant, club, team, ct), cancellationToken)
                            ?? throw new InvalidOperationException($"The {what} wasn't found after create.");
                    }

                    var changed = await CompleteAsync(context, tenantId, club, team, state, clubIndex, what, cancellationToken);
                    inserted += created ? 1 : 0;
                    updated += !created && changed ? 1 : 0;
                }
            }
        }

        return new SeedOutcome(inserted, updated, Deactivated: 0);
    }

    /// <summary>Runs each step the team still lacks: the rename, the badge, archiving; whether any ran.</summary>
    private static async Task<bool> CompleteAsync(
        SeedContext context, Guid tenantId, DemoClub club, DemoTeam team, TeamState state, int palette, string what, CancellationToken cancellationToken)
    {
        var id = state.Id;
        var changed = false;

        async Task RunAsync(ActionDefinition action, Func<IServiceProvider, CancellationToken, Task> command)
        {
            await context.RunCommandAsync(
                action,
                tenantId,
                async (services, ct) =>
                {
                    await command(services, ct);
                    return true;
                },
                cancellationToken);
            changed = true;
        }

        if (club.FirstName is { } first && state.NameKey == Team.NameKeyOf(first.Name))
        {
            await RunAsync(RegistryActions.TeamRename, async (services, ct) =>
            {
                var request = new RenameTeamRequest(club.Name, club.NameSince) { ShortName = club.ShortName, Code = club.Code };
                await DemoRegistryData.ValidateAsync(services, request, what, ct);
                DemoRegistryData.ThrowIfRefused(
                    await services.GetRequiredService<RenameTeamHandler>().HandleAsync(id, request, IfMatch.None, ct), $"the rename of {what}");
            });
        }

        if (team.Badge && !state.HasBadge)
        {
            await RunAsync(RegistryActions.TeamBadgeSet, async (services, ct) =>
            {
                using var file = new MemoryStream(PlaceholderPng.Logo(_badgeSize, palette));
                DemoRegistryData.ThrowIfRefused(
                    await services.GetRequiredService<SetTeamImageHandler>().HandleAsync(id, TeamImage.Badge, file, IfMatch.None, ct),
                    $"the badge of {what}");
            });
        }

        if (team.ArchiveReason is { } reason && state.Status != TeamStatus.Archived)
        {
            await RunAsync(RegistryActions.TeamStatusChange, async (services, ct) =>
            {
                var request = new ChangeTeamStatusRequest(TeamStatus.Archived, reason);
                await DemoRegistryData.ValidateAsync(services, request, what, ct);
                DemoRegistryData.ThrowIfRefused(
                    await services.GetRequiredService<ChangeTeamStatusHandler>().HandleAsync(id, request, IfMatch.None, ct), $"archiving {what}");
            });
        }

        return changed;
    }

    /// <summary><c>team.create</c>'s request: the first name when the club was renamed since, else the club's name.</summary>
    private static async Task<CreateTeamRequest> CreateRequestAsync(
        IServiceProvider services, string variant, DemoClub club, DemoTeam team, string what, CancellationToken cancellationToken)
    {
        var organisationId = club.Organisation is { } organisation
            ? await DemoOrganisationSeeder.FindIdAsync(services, organisation, cancellationToken)
                ?? throw new InvalidOperationException($"The organisation of {what}, {organisation}, isn't seeded: list it in demo-organisations.json.")
            : (Guid?)null;
        var homeVenueId = club.HomeVenue is { } venue
            ? await DemoVenueSeeder.FindAsync(services, venue, cancellationToken)
                ?? throw new InvalidOperationException($"The home venue of {what}, {venue}, isn't seeded: list it in demo-venues.json.")
            : (Guid?)null;
        var first = club.FirstName;
        return new CreateTeamRequest(
            variant,
            first?.Name ?? club.Name,
            team.GenderCategory,
            team.AgeGroup,
            Level: team.Level,
            ShortName: first is null ? club.ShortName : first.ShortName,
            OrganisationId: organisationId,
            Code: first is null ? club.Code : first.Code,
            PrimaryColour: club.PrimaryColour,
            SecondaryColour: club.SecondaryColour,
            HomeVenueId: homeVenueId,
            FoundedYear: club.FoundedYear,
            NameSince: first?.NameSince ?? club.NameSince,
            Slug: team.Slug);
    }

    /// <summary>
    /// The tenant's team of the variant and category whose current name, or a former name, is the club's name or first
    /// name (the earliest created), or null.
    /// </summary>
    private static async Task<TeamState?> FindAsync(
        IServiceProvider services, string variant, DemoClub club, DemoTeam team, CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<RegistryDbContext>();
        string[] keys = club.FirstName is { } first ? [Team.NameKeyOf(club.Name), Team.NameKeyOf(first.Name)] : [Team.NameKeyOf(club.Name)];
        var level = team.Level ?? TeamLevel.First;

        // IDs are version 7: the earliest created comes first.
        return await context.Set<Team>().AsNoTracking()
            .Where(t => t.Variant == variant && t.GenderCategory == team.GenderCategory && t.AgeGroup == team.AgeGroup && t.Level == level)
            .Where(t => keys.Contains(t.NameKey)
                || context.Set<TeamAlias>().Any(a => a.TeamId == t.Id && a.Kind == AliasKind.FormerName && keys.Contains(a.NameKey)))
            .OrderBy(t => t.Id)
            .Select(t => new TeamState(t.Id, t.NameKey, t.Status, t.BadgeAssetId != null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<DemoTenantTeams> Load()
    {
        var tenants = DemoRegistryData.Load<DemoTeamsFile>(_file).Tenants;
        var renamed = tenants.SelectMany(t => t.Clubs).FirstOrDefault(c => c.FirstName is not null && c.NameSince is null);
        return renamed is null
            ? tenants
            : throw new InvalidOperationException($"Demo club {renamed.Name} in {_file} has a first name, so it needs the new name's 'nameSince'.");
    }

    private sealed record TeamState(Guid Id, string NameKey, TeamStatus Status, bool HasBadge);
}

/// <summary>The shape of <c>demo-teams.json</c>; <c>_comment</c> says what the file is.</summary>
internal sealed record DemoTeamsFile(
    [property: JsonPropertyName("_comment")] string Comment,
    IReadOnlyList<DemoTenantTeams> Tenants);

internal sealed record DemoTenantTeams(string Slug, string Variant, IReadOnlyList<DemoClub> Clubs);

/// <summary>
/// A club's teams of the variant: the name, short name, code, colours, home venue and founded year are the club's.
/// With <see cref="FirstName"/>, the teams are created under it and renamed to <see cref="Name"/> from
/// <see cref="NameSince"/>; without, <see cref="NameSince"/> (optional) is the name's first day.
/// </summary>
internal sealed record DemoClub(
    string Name,
    IReadOnlyList<DemoTeam> Teams,
    string? Organisation = null,
    string? ShortName = null,
    string? Code = null,
    string? PrimaryColour = null,
    string? SecondaryColour = null,
    string? HomeVenue = null,
    int? FoundedYear = null,
    DateOnly? NameSince = null,
    DemoFirstName? FirstName = null);

internal sealed record DemoFirstName(string Name, DateOnly NameSince, string? ShortName = null, string? Code = null);

/// <summary>A team of the club: its category, an optional slug, a badge, and archiving with a reason.</summary>
internal sealed record DemoTeam(
    GenderCategory GenderCategory,
    AgeGroup AgeGroup,
    TeamLevel? Level = null,
    string? Slug = null,
    bool Badge = false,
    string? ArchiveReason = null);
