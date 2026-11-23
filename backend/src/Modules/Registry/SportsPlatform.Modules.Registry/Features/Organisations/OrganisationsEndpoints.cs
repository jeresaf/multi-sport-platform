using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations;

/// <summary><c>/api/admin/v1/organisations</c> (S07 D19): the organisations of the request's tenant (<c>X-Tenant-Id</c>).</summary>
internal static class OrganisationsEndpoints
{
    public const string Tag = "Organisations";

    /// <summary>The audit entity type of an organisation.</summary>
    public const string EntityType = "Organisation";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/organisations").WithTags(Tag);

    public static string PathOf(Guid organisationId) => $"/api/admin/v1/organisations/{organisationId}";
}

/// <summary>What every change to the tenant's organisations shares (S07 D21): the lock.</summary>
internal static class OrganisationChanges
{
    /// <summary>
    /// Holds <c>registry.organisations:&lt;tenantId&gt;</c> until the transaction ends: creates, changes to a compared field
    /// or the slug, and new parents (S07-T2c) queue on it while they check slugs, duplicates and cycles and save.
    /// </summary>
    public static async Task LockOrganisationsAsync(RegistryDbContext context, Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"registry.organisations:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
