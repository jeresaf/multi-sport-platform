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

namespace SportsPlatform.Modules.Registry.Features.Organisations.ListOrganisationStatusChanges;

/// <summary>One row of an organisation's status history (S07 D12, D16: the reasons and the history are internal).</summary>
public sealed record OrganisationStatusChangeResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] OrganisationStatus? FromStatus,
    [property: PersonalData(PersonalDataClass.Internal)] OrganisationStatus ToStatus,
    [property: PersonalData(PersonalDataClass.Internal)] string? Reason,
    [property: PersonalData(PersonalDataClass.Internal)] ActorKind ActorKind,
    [property: PersonalData(PersonalDataClass.Internal)] Guid ActorId,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset OccurredAt);

public sealed record ListOrganisationStatusChangesResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<OrganisationStatusChangeResponse> Items);

/// <summary>
/// An organisation's whole status history, newest first (S07 D12, D19); an organisation has few rows, so it is not paged.
/// </summary>
public sealed class ListOrganisationStatusChangesHandler(RegistryDbContext context)
{
    public async Task<Result<ListOrganisationStatusChangesResponse>> HandleAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Organisation>().AnyAsync(o => o.Id == organisationId, cancellationToken))
        {
            return RegistryErrors.OrganisationNotFound;
        }

        var items = await context.Set<OrganisationStatusChange>().AsNoTracking()
            .Where(c => c.OrganisationId == organisationId)
            .OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.Id)
            .Select(c => new OrganisationStatusChangeResponse(
                c.Id, c.FromStatus, c.ToStatus, c.Reason, c.ActorKind, c.ActorId, c.OccurredAt))
            .ToListAsync(cancellationToken);
        return Result.Success(new ListOrganisationStatusChangesResponse(items));
    }
}

internal static class ListOrganisationStatusChangesEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations) => organisations
        .MapGet("/{organisationId:guid}/status-changes", HandleAsync)
        .WithName("ListOrganisationStatusChanges")
        .WithSummary("List an organisation's status history")
        .WithDescription("Every status the organisation has had, newest first, with who changed it, when and why.")
        .RequireAction(RegistryActions.OrganisationRead)
        .ProducesErrors(RegistryErrors.OrganisationNotFound);

    private static async Task<Results<Ok<ListOrganisationStatusChangesResponse>, ProblemHttpResult>> HandleAsync(
        Guid organisationId,
        ListOrganisationStatusChangesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(organisationId, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
