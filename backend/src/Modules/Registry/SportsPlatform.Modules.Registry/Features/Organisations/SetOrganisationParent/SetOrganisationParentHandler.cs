using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.SetOrganisationParent;

/// <summary>
/// Sets or clears an organisation's parent (S07 D5): the open link ends on the tenant's today and a new one opens from
/// it; the link and the audit entry (with the reason) commit together. A new parent is checked under
/// <c>registry.organisations:&lt;tenantId&gt;</c>, which every parent change holds (D21): it must be another organisation
/// of the tenant, not dissolved or archived (<c>ORGANISATION_PARENT_UNAVAILABLE</c>), and not the organisation or one of
/// its descendants (<c>ORGANISATION_PARENT_CYCLE</c>), so two changes that would close a cycle together run one after the
/// other and the second sees the first's link. The parent is loaded tracked, so its new link moves its version too: a
/// parent dissolved or archived at the same moment is a conflict, never a link to it (owner, S07-T2c plan). Clearing
/// the parent needs no lock: it can't close a cycle. Setting the parent the organisation already has changes nothing.
/// </summary>
public sealed class SetOrganisationParentHandler(
    RegistryDbContext context, ICurrentActor actor, AuditScope audit, TenantToday tenantToday, ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid organisationId, SetOrganisationParentRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("An organisation's parent is set under the request's tenant.");
        var reason = request.Reason!.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId, reason, new OrganisationParentAuditMetadata(request.ParentId));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var organisation = await context.Set<Organisation>()
            .Include(o => o.ParentLinks)
            .SingleOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return new(RegistryErrors.OrganisationNotFound);
        }

        if (!RowVersion.IsSatisfied(
                context.Entry(organisation).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return new(RegistryErrors.OrganisationConcurrencyConflict);
        }

        if (request.ParentId == organisation.CurrentParentId)
        {
            return null;
        }

        Organisation? parent = null;
        if (request.ParentId is { } parentId)
        {
            if (await CheckParentAsync(organisation, parentId, tenantId, cancellationToken) is { } refusal)
            {
                return refusal;
            }

            parent = await context.Set<Organisation>().SingleOrDefaultAsync(o => o.Id == parentId, cancellationToken);
            if (parent is null)
            {
                return new(RegistryErrors.OrganisationParentUnavailable, "parentId");
            }
        }

        var today = await tenantToday.GetAsync(cancellationToken);
        var set = organisation.SetParent(parent, reason, actor, today, timeProvider.GetUtcNow());
        if (set.IsFailure)
        {
            return new(set.Error, set.Error.Code.StartsWith("ORGANISATION_PARENT_", StringComparison.Ordinal) ? "parentId" : null);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }

    // The organisation takes a new parent, and the parent isn't the organisation or below it: read under the lock, so
    // every link committed before it is seen and none can be added until this transaction ends.
    private async Task<FieldRefusal?> CheckParentAsync(
        Organisation organisation, Guid parentId, Guid tenantId, CancellationToken cancellationToken)
    {
        var accepts = organisation.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        if (parentId == organisation.Id)
        {
            return new(RegistryErrors.OrganisationParentCycle, "parentId");
        }

        await OrganisationChanges.LockOrganisationsAsync(context, tenantId, cancellationToken);
        return await IsAncestorOfAsync(organisation.Id, parentId, cancellationToken)
            ? new(RegistryErrors.OrganisationParentCycle, "parentId")
            : null;
    }

    // Walks up the open links from the proposed parent: the organisation among them means the parent is one of its
    // descendants. UNION drops rows already seen, so the walk ends even over data that would hold a cycle. RLS keeps it
    // to the tenant's links.
    private async Task<bool> IsAncestorOfAsync(Guid organisationId, Guid parentId, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<bool>($"""
            WITH RECURSIVE ancestors(id) AS (
                SELECT {parentId}
                UNION
                SELECT l.parent_id
                FROM registry.organisation_parent_links l
                JOIN ancestors a ON l.organisation_id = a.id
                WHERE l.valid_to IS NULL
            )
            SELECT EXISTS (SELECT 1 FROM ancestors WHERE id = {organisationId}) AS "Value"
            """).SingleAsync(cancellationToken);
}

/// <summary>The audit metadata of a parent change (S07 D5): the parent asked for, by ID (null to clear it).</summary>
public sealed record OrganisationParentAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ParentId);
