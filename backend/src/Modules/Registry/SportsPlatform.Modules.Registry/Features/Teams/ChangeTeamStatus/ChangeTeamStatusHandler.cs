using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.Teams.ChangeTeamStatus;

/// <summary>
/// Moves a team along its lifecycle (S07 D12): the status-history row and the audit entry (with the reason) commit
/// together. A move the current status doesn't allow is <c>TEAM_STATUS_INVALID</c>; unarchiving goes back only to the
/// status before archiving (Q55). A move into a status that counts toward <c>MAX_TEAMS</c> from one that doesn't
/// (reform, or unarchiving to <c>ACTIVE</c>, <c>INACTIVE</c> or <c>SUSPENDED</c>) checks the quota under
/// <c>registry.teams:&lt;tenantId&gt;</c>, as a create does (D14): at the limit <c>ENTITLEMENT_QUOTA_EXCEEDED</c> ("Your
/// plan allows N teams."). Every move to a status that counts takes the lock before the team is read (review #128 R1):
/// two moves of one team then queue, and the second reads the first's result, so it is answered with the version's
/// conflict, never with the quota the first one used. Other moves take no lock. Whether the variant is still enabled
/// doesn't matter (T3 acceptance). The version guards the move: a team changed since the read is a conflict.
/// </summary>
public sealed class ChangeTeamStatusHandler(
    RegistryDbContext context,
    TeamCount teams,
    IEntitlements entitlements,
    ITenantContext tenantContext,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    /// <summary>Null when saved and committed; otherwise the refusal.</summary>
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, ChangeTeamStatusRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("A team is changed under the request's tenant.");
        var reason = request.Reason!.Trim();
        audit.Describe(TeamsEndpoints.EntityType, teamId, reason);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var to = request.Status!.Value;
        if (TeamCount.Counts(to))
        {
            await TeamChanges.LockTeamsAsync(context, tenantId, cancellationToken);
        }

        // Read after the lock (when taken): a new statement, so in READ COMMITTED it sees every move committed before it.
        // The history is read whole: unarchiving needs the status before archiving, and a team has few rows.
        var team = await context.Set<Team>()
            .Include(t => t.StatusHistory)
            .SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
        {
            return RegistryErrors.TeamNotFound.ToProblem();
        }

        if (!RowVersion.IsSatisfied(context.Entry(team).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return RegistryErrors.TeamConcurrencyConflict.ToProblem();
        }

        var from = team.Status;
        var changed = team.ChangeStatus(to, reason, actor, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return changed.Error.ToProblem();
        }

        if (!TeamCount.Counts(from) && TeamCount.Counts(to))
        {
            // Counted under the lock taken above. The team itself doesn't count yet (its new status isn't saved).
            if (await TeamChanges.CheckQuotaAsync(teams, entitlements, cancellationToken) is { } quota)
            {
                return quota.ToProblem();
            }
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error.ToProblem();
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }
}
