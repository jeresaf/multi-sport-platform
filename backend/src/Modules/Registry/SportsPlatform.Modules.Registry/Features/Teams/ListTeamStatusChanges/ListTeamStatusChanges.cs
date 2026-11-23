using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.ListTeamStatusChanges;

/// <summary>One row of a team's status history (S07 D12, D16: the reasons and the history are internal).</summary>
public sealed record TeamStatusChangeResponse(
    [property: PersonalData(PersonalDataClass.Internal)] Guid Id,
    [property: PersonalData(PersonalDataClass.Internal)] TeamStatus? FromStatus,
    [property: PersonalData(PersonalDataClass.Internal)] TeamStatus ToStatus,
    [property: PersonalData(PersonalDataClass.Internal)] string? Reason,
    [property: PersonalData(PersonalDataClass.Internal)] ActorKind ActorKind,
    [property: PersonalData(PersonalDataClass.Internal)] Guid ActorId,
    [property: PersonalData(PersonalDataClass.Internal)] DateTimeOffset OccurredAt);

public sealed record ListTeamStatusChangesResponse(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<TeamStatusChangeResponse> Items);

/// <summary>
/// A team's whole status history, newest first (S07 D12, D19); a team has few rows, so it is not paged.
/// </summary>
public sealed class ListTeamStatusChangesHandler(RegistryDbContext context)
{
    public async Task<Result<ListTeamStatusChangesResponse>> HandleAsync(Guid teamId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Team>().AnyAsync(t => t.Id == teamId, cancellationToken))
        {
            return RegistryErrors.TeamNotFound;
        }

        var items = await context.Set<TeamStatusChange>().AsNoTracking()
            .Where(c => c.TeamId == teamId)
            .OrderByDescending(c => c.OccurredAt).ThenByDescending(c => c.Id)
            .Select(c => new TeamStatusChangeResponse(
                c.Id, c.FromStatus, c.ToStatus, c.Reason, c.ActorKind, c.ActorId, c.OccurredAt))
            .ToListAsync(cancellationToken);
        return Result.Success(new ListTeamStatusChangesResponse(items));
    }
}

internal static class ListTeamStatusChangesEndpoint
{
    public static void Map(IEndpointRouteBuilder teams) => teams
        .MapGet("/{teamId:guid}/status-changes", HandleAsync)
        .WithName("ListTeamStatusChanges")
        .WithSummary("List a team's status history")
        .WithDescription("Every status the team has had, newest first, with who changed it, when and why.")
        .RequireAction(RegistryActions.TeamRead)
        .ProducesErrors(RegistryErrors.TeamNotFound);

    private static async Task<Results<Ok<ListTeamStatusChangesResponse>, ProblemHttpResult>> HandleAsync(
        Guid teamId,
        ListTeamStatusChangesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(teamId, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : TypedResults.Ok(result.Value);
    }
}
