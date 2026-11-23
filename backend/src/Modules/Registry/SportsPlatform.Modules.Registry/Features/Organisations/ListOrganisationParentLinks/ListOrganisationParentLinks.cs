using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisationParentLinks;

/// <summary>
/// One link of an organisation to a parent (S07 D5, D19; D16: the links and their reasons are internal). The parent is a
/// summary of the tenant's organisation, whatever its status now; <c>validTo</c> is null on the current link.
/// </summary>
public sealed record OrganisationParentLinkResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] OrganisationParentSummary Parent,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly ValidFrom,
    [property: PersonalData(PersonalDataClass.Internal)] DateOnly? ValidTo,
    [property: PersonalData(PersonalDataClass.Internal)] string Reason,
    [property: PersonalData(PersonalDataClass.Internal)] ActorKind ActorKind,
    [property: PersonalData(PersonalDataClass.Internal)] Guid ActorId,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset SetAt);

/// <summary>An organisation another points at (S07 D16: id, display name, slug), with its status now.</summary>
public sealed record OrganisationParentSummary(
    [property: PersonalData(PersonalDataClass.Public)] Guid Id,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] OrganisationStatus Status);

public sealed record ListOrganisationParentLinksResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<OrganisationParentLinkResponse> Items);

/// <summary>
/// Every parent an organisation has had, newest first (by when it was set, then ID); an organisation has few, so the
/// list is not paged.
/// </summary>
public sealed class ListOrganisationParentLinksHandler(RegistryDbContext context)
{
    public async Task<Result<ListOrganisationParentLinksResponse>> HandleAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Organisation>().AnyAsync(o => o.Id == organisationId, cancellationToken))
        {
            return RegistryErrors.OrganisationNotFound;
        }

        var items = await context.Set<OrganisationParentLink>().AsNoTracking()
            .Where(l => l.OrganisationId == organisationId)
            .Join(context.Set<Organisation>(), l => l.ParentId, o => o.Id, (l, o) => new { Link = l, Parent = o })
            .OrderByDescending(row => row.Link.SetAt).ThenByDescending(row => row.Link.Id)
            .Select(row => new OrganisationParentLinkResponse(
                row.Link.Id,
                new OrganisationParentSummary(row.Parent.Id, row.Parent.DisplayName, row.Parent.Slug, row.Parent.Status),
                row.Link.ValidFrom,
                row.Link.ValidTo,
                row.Link.Reason,
                row.Link.ActorKind,
                row.Link.ActorId,
                row.Link.SetAt))
            .ToListAsync(cancellationToken);
        return Result.Success(new ListOrganisationParentLinksResponse(items));
    }
}

internal static class ListOrganisationParentLinksEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapGet("/{organisationId:guid}/parent-links", HandleAsync)
        .WithName("ListOrganisationParentLinks")
        .WithSummary("List an organisation's parent history")
        .WithDescription(
            "Every parent the organisation has had, newest first, with the dates of each link (the tenant's dates; " +
            "`validTo` is null on the current one), who set it, when and why. A link opened and ended on the same day " +
            "keeps both dates.")
        .RequireAction(RegistryActions.OrganisationRead)
        .ProducesErrors(RegistryErrors.OrganisationNotFound);

    private static async Task<Results<Ok<ListOrganisationParentLinksResponse>, ProblemHttpResult>> HandleAsync(
        Guid organisationId,
        ListOrganisationParentLinksHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(organisationId, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
