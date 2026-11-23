using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.GetOrganisation;
using SportsPlatform.Modules.Registry.Features.People;
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

/// <summary>
/// What every change to the tenant's organisations shares (S07 D21): the lock, and for the alias and identifier changes
/// (S07-T2d) the load with the version check and the answer.
/// </summary>
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

    /// <summary>
    /// The organisation with its aliases, or <c>ORGANISATION_NOT_FOUND</c>, or <c>ORGANISATION_CONCURRENCY_CONFLICT</c>
    /// when <paramref name="version"/> or <paramref name="ifMatch"/> names another version.
    /// </summary>
    public static async Task<(Organisation? Organisation, FieldRefusal? Refusal)> LoadAsync(
        RegistryDbContext context, Guid organisationId, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        var organisation = await context.Set<Organisation>()
            .Include(o => o.Aliases)
            .SingleOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return (null, new(RegistryErrors.OrganisationNotFound));
        }

        return RowVersion.IsSatisfied(context.Entry(organisation).Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch)
            ? (organisation, null)
            : (null, new(RegistryErrors.OrganisationConcurrencyConflict));
    }

    /// <summary>Reads <c>If-Match</c>, runs <paramref name="change"/>, and answers the refusal or the saved organisation.</summary>
    public static async Task<Results<Ok<OrganisationResponse>, ProblemHttpResult>> AnswerAsync(
        Guid organisationId,
        GetOrganisationHandler read,
        HttpRequest httpRequest,
        HttpResponse response,
        Func<IfMatch, Task<FieldRefusal?>> change)
    {
        if (!RowVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (await change(ifMatch) is { } refusal)
        {
            return refusal.ToProblem();
        }

        // Saved: the read is not cancelled with the request.
        var organisation = await read.HandleAsync(organisationId, CancellationToken.None);
        organisation.Value.SetETag(response);
        return TypedResults.Ok(organisation.Value);
    }
}
