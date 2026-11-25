using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.People.ExternalIdentifiers;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Registry.Features.Teams.ExternalIdentifiers;

/// <summary>
/// Adds an external identifier to a team (S07 D10): refused on a disbanded or archived team. Holding
/// <c>registry.teams:&lt;tenantId&gt;</c> (D21), it runs the exact check: a value with this authority that any team has or
/// had, this one included, is <c>TEAM_EXTERNAL_ID_EXISTS</c>; other entity types' identifiers don't clash. The
/// national-document authorities are a person rule only. The identifier has no foreign key to the team, so the team is
/// marked changed (S06 D11).
/// </summary>
public sealed class AddTeamExternalIdentifierHandler(RegistryDbContext context, ITenantContext tenantContext, AuditScope audit)
{
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, AddTeamExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A team is changed under the request's tenant.");
        var authority = request.Authority!;
        var value = request.Value!.Trim();
        audit.Describe(TeamsEndpoints.EntityType, teamId);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, request.Version, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        var accepts = team.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error.ToProblem();
        }

        await TeamChanges.LockTeamsAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every identifier committed before it.
        if (await context.Set<ExternalIdentifier>().AnyAsync(
                identifier => identifier.EntityType == ExternalIdentifierEntityType.Team
                    && identifier.Authority == authority && identifier.Value == value,
                cancellationToken))
        {
            return new FieldRefusal(RegistryErrors.TeamExternalIdExists, "value").ToProblem();
        }

        var identifier = ExternalIdentifier.ForTeam(team.Id, authority, value, request.ValidFrom);
        context.Add(identifier);
        context.MarkChanged(team);
        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, authority));
        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error.ToProblem();
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ExternalIdentifierConfiguration.ValueIndex,
        })
        {
            // Not reachable while every write of a team identifier holds the lock; kept so a missed lock can't answer 500.
            return new FieldRefusal(RegistryErrors.TeamExternalIdExists, "value").ToProblem();
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }
}

/// <summary>
/// Ends a team's external identifier (S07 D10), whatever the team's status: <c>valid_to</c> is set, the row stays and the
/// value is never reused. The date defaults to the tenant's today and can't be after it or before the identifier's start.
/// </summary>
public sealed class EndTeamExternalIdentifierHandler(RegistryDbContext context, TenantToday tenantToday, AuditScope audit)
{
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, Guid identifierId, EndTeamExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        audit.Describe(TeamsEndpoints.EntityType, teamId, reason);

        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, request.Version, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        var identifier = await context.Set<ExternalIdentifier>().SingleOrDefaultAsync(
            i => i.Id == identifierId && i.EntityType == ExternalIdentifierEntityType.Team && i.EntityId == teamId,
            cancellationToken);
        if (identifier is null)
        {
            return RegistryErrors.TeamExternalIdNotFound.ToProblem();
        }

        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, identifier.Authority));
        var today = await tenantToday.GetAsync(cancellationToken);
        var ended = identifier.End(request.ValidTo ?? today, today);
        if (ended.IsFailure)
        {
            return new FieldRefusal(ended.Error, ended.Error.Code == RegistryErrors.TeamExternalIdValidityInvalid.Code ? "validTo" : null)
                .ToProblem();
        }

        context.MarkChanged(team);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? saved.Error.ToProblem() : null;
    }
}
