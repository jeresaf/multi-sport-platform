using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary>
/// The scope's tenant's teams for <c>MAX_TEAMS</c> (S07 D14; owner, Q50): those <c>ACTIVE</c>, <c>INACTIVE</c> or
/// <c>SUSPENDED</c>. Disbanded and archived teams don't count, so disbanding or archiving frees a place, and reforming
/// or unarchiving takes one (S07-T3b). Read under RLS; a create counts under <c>registry.teams:&lt;tenantId&gt;</c>.
/// </summary>
public sealed class TeamCount(RegistryDbContext context) : IQuotaUsage
{
    public const string Code = "MAX_TEAMS";

    /// <summary>Whether a team in <paramref name="status"/> takes a place of the quota.</summary>
    public static bool Counts(TeamStatus status) => status is TeamStatus.Active or TeamStatus.Inactive or TeamStatus.Suspended;

    public Task<int> CountAsync(CancellationToken cancellationToken) => context.Set<Team>()
        .CountAsync(
            t => t.Status == TeamStatus.Active || t.Status == TeamStatus.Inactive || t.Status == TeamStatus.Suspended,
            cancellationToken);
}
