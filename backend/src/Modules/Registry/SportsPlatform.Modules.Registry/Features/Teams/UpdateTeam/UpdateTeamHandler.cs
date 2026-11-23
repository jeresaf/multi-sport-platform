using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Registry.Features.Teams.UpdateTeam;

/// <summary>
/// Replaces a team's editable fields (S07 D11), whatever its status (a correction, as organisations' and venues'), and
/// whether or not its variant is still enabled (T3 acceptance: a disabled variant keeps its teams). A name other than
/// the current one is <c>TEAM_RENAME_REQUIRED</c> (D8). The details' own rules come next (an organisation or home venue
/// is checked only when it changes, D6; untracked), then the version. A change to the slug or to what the duplicate
/// warning compares (the short name's key, the category, the organisation) holds <c>registry.teams:&lt;tenantId&gt;</c>
/// while the slug is checked and the warning runs again, the team itself left out (D13, D21): teams that look like the
/// changed one refuse the update with <c>TEAM_DUPLICATE_REVIEW_REQUIRED</c> and the candidates until the request says
/// <c>duplicatesReviewed</c>; the audit entry names them and the reason. The status doesn't change, so the quota isn't
/// checked.
/// </summary>
public sealed class UpdateTeamHandler(
    RegistryDbContext context,
    TeamDetailsRules rules,
    TeamDuplicateFinder finder,
    TenantToday tenantToday,
    ITenantContext tenantContext,
    AuditScope audit)
{
    /// <summary>Null when saved and committed; otherwise the refusal.</summary>
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, UpdateTeamRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("A team is changed under the request's tenant.");

        // Described before anything can refuse, so a refused update is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(TeamsEndpoints.EntityType, teamId, reason);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var team = await context.Set<Team>().SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
        {
            return RegistryErrors.TeamNotFound.ToProblem();
        }

        if (request.Name is not null && !string.Equals(request.Name.Trim(), team.Name, StringComparison.Ordinal))
        {
            return new FieldRefusal(RegistryErrors.TeamRenameRequired, "name").ToProblem();
        }

        // The details' rules come before the version, as organisations': a kept reference isn't checked again (D6).
        var today = await tenantToday.GetAsync(cancellationToken);
        var (details, refusal) = await rules.CheckAsync(request, team.Name, team, today, cancellationToken);
        if (details is null)
        {
            return refusal!.ToProblem();
        }

        if (!RowVersion.IsSatisfied(context.Entry(team).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return RegistryErrors.TeamConcurrencyConflict.ToProblem();
        }

        var slug = request.Slug ?? team.Slug;
        var slugChanges = slug != team.Slug;
        var comparedFieldsChange = team.ChangesComparedFields(details);
        if (slugChanges || comparedFieldsChange)
        {
            await TeamChanges.LockTeamsAsync(context, tenantId, cancellationToken);
        }

        // Read after the lock: a new statement, so in READ COMMITTED it sees every team committed before it.
        if (slugChanges && await context.Set<Team>().AnyAsync(t => t.Slug == slug && t.Id != teamId, cancellationToken))
        {
            return new FieldRefusal(RegistryErrors.TeamSlugTaken, "slug").ToProblem();
        }

        if (comparedFieldsChange
            && await ReviewDuplicatesAsync(team, details, request.DuplicatesReviewed, cancellationToken) is { } stop)
        {
            return stop;
        }

        team.Update(details, slug);
        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error.ToProblem();
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TeamConfiguration.SlugIndex,
        })
        {
            // Not reachable while every write of a slug holds the lock; kept so a missed lock can't answer 500.
            return new FieldRefusal(RegistryErrors.TeamSlugTaken, "slug").ToProblem();
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }

    // D13's warning for the changed details, the team itself left out; null to go on.
    private async Task<ProblemHttpResult?> ReviewDuplicatesAsync(
        Team team, TeamDetails details, bool reviewed, CancellationToken cancellationToken)
    {
        var candidates = await finder.FindAsync(CreateTeamHandler.InputOf(team.Variant, details, team.Id), cancellationToken);
        audit.Describe(metadata: new TeamDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new TeamDuplicateCandidateAudit(candidate.TeamId, candidate.Confidence))]));
        if (candidates.Count == 0 || reviewed)
        {
            return null;
        }

        var problem = new FieldRefusal(RegistryErrors.TeamDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
        problem.ProblemDetails.Extensions[CreateTeamHandler.CandidatesExtension] =
            candidates.Select(TeamDuplicateCandidateResponse.From).ToList();
        return problem;
    }
}
