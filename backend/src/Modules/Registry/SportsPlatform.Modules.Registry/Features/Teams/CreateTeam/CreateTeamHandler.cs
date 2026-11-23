using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Contracts.Sports;

namespace SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;

/// <summary>
/// Creates a team (S07 D6–D9, D13, D14, D21). The variant must be enabled for the tenant (<c>TEAM_SPORT_NOT_ENABLED</c>),
/// <c>nameSince</c> not after the tenant's today, and the details' own rules come next. In a transaction holding
/// <c>registry.teams:&lt;tenantId&gt;</c>, the slug is checked (or proposed from the name and category with <c>-2</c>,
/// <c>-3</c>… on a clash), <c>MAX_TEAMS</c> is checked against the teams that count, and the duplicate warning runs:
/// teams that look like this one refuse the create with <c>TEAM_DUPLICATE_REVIEW_REQUIRED</c> and the candidates,
/// until the request says <c>duplicatesReviewed</c>. The audit entry names the candidates shown and the reason. Two
/// creates queue on the lock: at the limit the second is refused, and the second of one team finds the first's.
/// </summary>
public sealed class CreateTeamHandler(
    RegistryDbContext context,
    TeamDetailsRules rules,
    TeamDuplicateFinder finder,
    TeamCount teams,
    IEntitlements entitlements,
    IEnabledSports enabledSports,
    TenantToday tenantToday,
    ITenantContext tenantContext,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    // The candidates of a refused create, in the Problem Details (S07 D13; documented as TeamDuplicateReviewProblem).
    public const string CandidatesExtension = "candidates";

    public async Task<(Guid? TeamId, ProblemHttpResult? Refusal)> HandleAsync(
        CreateTeamRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("A team is created under the request's tenant.");

        // Described before anything can refuse, so a refused create is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(TeamsEndpoints.EntityType, reason: reason);

        var variant = request.Variant!.Trim();
        if (!await enabledSports.IsEnabledAsync(variant, cancellationToken))
        {
            return (null, new FieldRefusal(RegistryErrors.TeamSportNotEnabled, "variant").ToProblem());
        }

        var today = await tenantToday.GetAsync(cancellationToken);
        if (request.NameSince > today)
        {
            return (null, new FieldRefusal(
                CommonErrors.InvalidRequest, "nameSince", "'nameSince' can't be after today (the tenant's date).").ToProblem());
        }

        var (details, refusal) = await rules.CheckAsync(request, request.Name, current: null, cancellationToken);
        if (details is null)
        {
            return (null, refusal!.ToProblem());
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await TeamChanges.LockTeamsAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every team committed before it.
        var slug = await SlugAsync(request.Slug, details, cancellationToken);
        if (slug is null)
        {
            return (null, new FieldRefusal(RegistryErrors.TeamSlugTaken, "slug").ToProblem());
        }

        if (await TeamChanges.CheckQuotaAsync(teams, entitlements, cancellationToken) is { } quota)
        {
            return (null, quota.ToProblem());
        }

        var candidates = await finder.FindAsync(InputOf(variant, details, teamId: null), cancellationToken);
        audit.Describe(metadata: new TeamDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new TeamDuplicateCandidateAudit(candidate.TeamId, candidate.Confidence))]));
        if (candidates.Count > 0 && !request.DuplicatesReviewed)
        {
            var problem = new FieldRefusal(RegistryErrors.TeamDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
            problem.ProblemDetails.Extensions[CandidatesExtension] = candidates.Select(TeamDuplicateCandidateResponse.From).ToList();
            return (null, problem);
        }

        var team = Team.Create(
            variant, details, request.NameSince ?? today, slug, TeamSource.Manual, actor, timeProvider.GetUtcNow());
        context.Add(team);
        audit.Describe(entityId: team.Id);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TeamConfiguration.SlugIndex,
        })
        {
            // Not reachable while every write of a slug holds the lock; kept so a missed lock can't answer 500.
            return (null, new FieldRefusal(RegistryErrors.TeamSlugTaken, "slug").ToProblem());
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return (team.Id, null);
    }

    /// <summary>What the duplicate warning compares for a team of <paramref name="variant"/> with <paramref name="details"/>.</summary>
    public static TeamDuplicateInput InputOf(string variant, TeamDetails details, Guid? teamId)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new(
            Team.NameKeyOf(details.Name),
            Team.OptionalNameKeyOf(details.ShortName),
            variant,
            details.Category,
            details.OrganisationId,
            teamId);
    }

    // The slug sent when it is free (null when taken), or the first free one proposed from the name and category.
    private async Task<string?> SlugAsync(string? sent, TeamDetails details, CancellationToken cancellationToken)
    {
        if (sent is not null)
        {
            return await context.Set<Team>().AnyAsync(t => t.Slug == sent, cancellationToken) ? null : sent;
        }

        var proposal = Team.ProposeSlug(details.Name, details.Category);
        var prefix = proposal + "-";
        var taken = (await context.Set<Team>().AsNoTracking()
                .Where(t => t.Slug == proposal || t.Slug.StartsWith(prefix))
                .Select(t => t.Slug)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var number = 1;
        while (taken.Contains(Slugs.WithSuffix(proposal, number)))
        {
            number++;
        }

        return Slugs.WithSuffix(proposal, number);
    }
}
