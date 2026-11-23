using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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

/// <summary>What every change to the tenant's teams shares (S07 D21): the lock and the quota.</summary>
internal static class TeamChanges
{
    /// <summary>
    /// Holds <c>registry.teams:&lt;tenantId&gt;</c> until the transaction ends: creates (and, with S07-T3b, changes to a
    /// compared field or the slug, reforms and unarchives) queue on it while they check slugs, duplicates and the quota
    /// and save.
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
}
