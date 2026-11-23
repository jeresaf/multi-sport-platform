using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ChangeOrganisationStatus;

/// <summary>
/// Moves an organisation along its lifecycle (S07 D12): the status-history row and the audit entry (with the reason)
/// commit together. A move the current status doesn't allow is <c>ORGANISATION_STATUS_INVALID</c>; unarchiving goes back
/// only to the status before archiving (Q55), so a dissolved organisation stays dissolved. Its teams are not touched.
/// The version guards the move: an organisation changed since the read is a conflict.
/// </summary>
public sealed class ChangeOrganisationStatusHandler(
    RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(
        Guid organisationId, ChangeOrganisationStatusRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var reason = request.Reason!.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId, reason);

        // The history is read whole: unarchiving needs the status before archiving, and an organisation has few rows.
        var organisation = await context.Set<Organisation>()
            .Include(o => o.StatusHistory)
            .SingleOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return Result.Failure(RegistryErrors.OrganisationNotFound);
        }

        if (!RowVersion.IsSatisfied(
                context.Entry(organisation).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return Result.Failure(RegistryErrors.OrganisationConcurrencyConflict);
        }

        var changed = organisation.ChangeStatus(request.Status!.Value, reason, actor, timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return changed;
        }

        return await context.SaveChangesResultAsync(cancellationToken);
    }
}
