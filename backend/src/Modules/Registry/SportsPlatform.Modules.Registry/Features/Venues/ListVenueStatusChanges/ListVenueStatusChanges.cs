using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.ListVenueStatusChanges;

/// <summary>One row of a venue's status history (S07 D12, D16: the reasons and the history are internal).</summary>
public sealed record VenueStatusChangeResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] VenueStatus? FromStatus,
    [property: PersonalData(PersonalDataClass.Internal)] VenueStatus ToStatus,
    [property: PersonalData(PersonalDataClass.Internal)] string? Reason,
    [property: PersonalData(PersonalDataClass.Internal)] ActorKind ActorKind,
    [property: PersonalData(PersonalDataClass.Internal)] Guid ActorId,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset OccurredAt);

public sealed record ListVenueStatusChangesResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<VenueStatusChangeResponse> Items);

/// <summary>A venue's whole status history, newest first (S07 D12, D19); a venue has few rows, so it is not paged.</summary>
public sealed class ListVenueStatusChangesHandler(RegistryDbContext context)
{
    public async Task<Result<ListVenueStatusChangesResponse>> HandleAsync(Guid venueId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Venue>().AnyAsync(v => v.Id == venueId, cancellationToken))
        {
            return RegistryErrors.VenueNotFound;
        }

        var items = await context.Set<VenueStatusChange>().AsNoTracking()
            .Where(c => c.VenueId == venueId)
            .OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.Id)
            .Select(c => new VenueStatusChangeResponse(c.Id, c.FromStatus, c.ToStatus, c.Reason, c.ActorKind, c.ActorId, c.OccurredAt))
            .ToListAsync(cancellationToken);
        return Result.Success(new ListVenueStatusChangesResponse(items));
    }
}

internal static class ListVenueStatusChangesEndpoint
{
    public static void Map(IEndpointRouteBuilder venues) => venues
        .MapGet("/{venueId:guid}/status-changes", HandleAsync)
        .WithName("ListVenueStatusChanges")
        .WithSummary("List a venue's status history")
        .WithDescription("Every status the venue has had, newest first, with who changed it, when and why.")
        .RequireAction(RegistryActions.VenueRead)
        .ProducesErrors(RegistryErrors.VenueNotFound);

    private static async Task<Results<Ok<ListVenueStatusChangesResponse>, ProblemHttpResult>> HandleAsync(
        Guid venueId,
        ListVenueStatusChangesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(venueId, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
