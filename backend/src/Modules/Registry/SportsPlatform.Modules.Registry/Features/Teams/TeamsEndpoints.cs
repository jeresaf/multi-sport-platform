using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.Teams.GetTeam;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary><c>/api/admin/v1/teams</c> (S07 D19): the teams of the request's tenant (<c>X-Tenant-Id</c>).</summary>
internal static class TeamsEndpoints
{
    public const string Tag = "Teams";

    /// <summary>The audit entity type of a team.</summary>
    public const string EntityType = "Team";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/teams").WithTags(Tag);

    public static string PathOf(Guid teamId) => $"/api/admin/v1/teams/{teamId}";
}

/// <summary>What every change to the tenant's teams shares (S07 D21): the lock, the quota, loading and answering.</summary>
internal static class TeamChanges
{
    /// <summary>
    /// Holds <c>registry.teams:&lt;tenantId&gt;</c> until the transaction ends: creates, updates that change a compared
    /// field or the slug, and moves to a status that counts toward <c>MAX_TEAMS</c> queue on it while they check slugs,
    /// duplicates and the quota and save.
    /// </summary>
    public static async Task LockTeamsAsync(RegistryDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"registry.teams:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }

    /// <summary>
    /// <c>ENTITLEMENT_QUOTA_EXCEEDED</c> ("Your plan allows N teams.") when one more team would pass the plan's
    /// <c>MAX_TEAMS</c> (S07 D14). Call under <see cref="LockTeamsAsync"/>, so two creates at the limit don't both pass.
    /// </summary>
    public static async Task<FieldRefusal?> CheckQuotaAsync(
        TeamCount teams, IEntitlements entitlements, CancellationToken cancellationToken)
    {
        var quota = await entitlements.CheckQuotaAsync(TeamCount.Code, await teams.CountAsync(cancellationToken), cancellationToken);
        if (quota.IsSuccess)
        {
            return null;
        }

        var limit = await entitlements.GetLimitAsync(TeamCount.Code, cancellationToken);
        return new(quota.Error, Detail: $"Your plan allows {limit} teams.");
    }

    /// <summary>
    /// The team with its aliases, or <c>TEAM_NOT_FOUND</c>, or <c>TEAM_CONCURRENCY_CONFLICT</c> when
    /// <paramref name="version"/> or <paramref name="ifMatch"/> names another version.
    /// </summary>
    public static async Task<(Team? Team, FieldRefusal? Refusal)> LoadAsync(
        RegistryDbContext context, Guid teamId, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        var team = await context.Set<Team>()
            .Include(t => t.Aliases)
            .SingleOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
        {
            return (null, new(RegistryErrors.TeamNotFound));
        }

        return RowVersion.IsSatisfied(context.Entry(team).Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch)
            ? (team, null)
            : (null, new(RegistryErrors.TeamConcurrencyConflict));
    }

    /// <summary>Reads <c>If-Match</c>, runs <paramref name="change"/>, and answers the refusal or the saved team.</summary>
    public static async Task<Results<Ok<TeamResponse>, ProblemHttpResult>> AnswerAsync(
        Guid teamId,
        GetTeamHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        Func<IfMatch, Task<ProblemHttpResult?>> change)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await change(ifMatch) is { } refusal)
        {
            return refusal;
        }

        // Saved and committed: the read is not cancelled with the request.
        var team = await read.HandleAsync(teamId, CancellationToken.None);
        team.Value.SetETag(response);
        return TypedResults.Ok(team.Value);
    }
}
