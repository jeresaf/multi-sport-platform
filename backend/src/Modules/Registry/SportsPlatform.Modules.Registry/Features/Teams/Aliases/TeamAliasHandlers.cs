using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.Aliases;

/// <summary>
/// Adds a nickname or spelling to a team (S07 D8, D10): refused on a disbanded or archived team and when the team is
/// already known by the same name key (<c>TEAM_ALIAS_EXISTS</c>). No duplicate warning runs (as organisations' and
/// venues'), and aliases don't feed later warnings (Q57 as built). The new alias row moves the team's version (S06 D11).
/// </summary>
public sealed class AddTeamAliasHandler(RegistryDbContext context, AuditScope audit)
{
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, AddTeamAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(TeamsEndpoints.EntityType, teamId);
        if (Team.NameKeyOf(request.Name).Length == 0)
        {
            return new FieldRefusal(RegistryErrors.TeamNameInvalid, "name").ToProblem();
        }

        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, request.Version, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        var added = team.AddAlias(request.Name!, request.Kind!.Value);
        if (added.IsFailure)
        {
            return new FieldRefusal(added.Error, added.Error.Code == RegistryErrors.TeamAliasExists.Code ? "name" : null).ToProblem();
        }

        audit.Describe(metadata: new TeamAliasAuditMetadata(added.Value.Id));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? saved.Error.ToProblem() : null;
    }
}

/// <summary>
/// Removes a team's nickname or spelling with a reason, in any status (S07 D10); the alias stays with who, when and why.
/// A former name can't be removed (<c>TEAM_ALIAS_FORMER_NAME</c>; owner, S07-T3c plan).
/// </summary>
public sealed class RemoveTeamAliasHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, Guid aliasId, RemoveTeamAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        audit.Describe(TeamsEndpoints.EntityType, teamId, reason, new TeamAliasAuditMetadata(aliasId));

        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, request.Version, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        var removed = team.RemoveAlias(aliasId, reason, actor, timeProvider.GetUtcNow());
        if (removed.IsFailure)
        {
            return removed.Error.ToProblem();
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? saved.Error.ToProblem() : null;
    }
}

/// <summary>The audit metadata of a team alias change: the alias by ID.</summary>
public sealed record TeamAliasAuditMetadata([property: PersonalData(PersonalDataClass.Internal)] Guid AliasId);
