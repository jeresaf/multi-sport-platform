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

namespace SportsPlatform.Modules.Registry.Features.Organisations.ExternalIdentifiers;

/// <summary>
/// Adds an external identifier to an organisation (S07 D10): refused on a dissolved or archived organisation. Holding
/// <c>registry.organisations:&lt;tenantId&gt;</c> (D21), it runs the exact check: a value with this authority that any organisation
/// has or had, this one included, is <c>ORGANISATION_EXTERNAL_ID_EXISTS</c>; people's identifiers are another entity type and
/// don't clash. The national-document authorities are a person rule only. The identifier has no foreign key to the
/// organisation, so the organisation is marked changed (S06 D11).
/// </summary>
public sealed class AddOrganisationExternalIdentifierHandler(RegistryDbContext context, ITenantContext tenantContext, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid organisationId, AddOrganisationExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("An organisation is changed under the request's tenant.");
        var authority = request.Authority!;
        var value = request.Value!.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var (organisation, refusal) = await OrganisationChanges.LoadAsync(context, organisationId, request.Version, ifMatch, cancellationToken);
        if (organisation is null)
        {
            return refusal;
        }

        var accepts = organisation.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        await OrganisationChanges.LockOrganisationsAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every identifier committed before it.
        if (await context.Set<ExternalIdentifier>().AnyAsync(
                identifier => identifier.EntityType == ExternalIdentifierEntityType.Organisation
                    && identifier.Authority == authority && identifier.Value == value,
                cancellationToken))
        {
            return new(RegistryErrors.OrganisationExternalIdExists, "value");
        }

        var identifier = ExternalIdentifier.ForOrganisation(organisation.Id, authority, value, request.ValidFrom);
        context.Add(identifier);
        context.MarkChanged(organisation);
        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, authority));
        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return new(saved.Error);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ExternalIdentifierConfiguration.ValueIndex,
        })
        {
            // Not reachable while every write of an organisation identifier holds the lock; kept so a missed lock can't answer 500.
            return new(RegistryErrors.OrganisationExternalIdExists, "value");
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }
}

/// <summary>
/// Ends an organisation's external identifier (S07 D10), whatever the organisation's status: <c>valid_to</c> is set, the row stays and
/// the value is never reused. The date defaults to the tenant's today and can't be after it or before the identifier's
/// start.
/// </summary>
public sealed class EndOrganisationExternalIdentifierHandler(RegistryDbContext context, TenantToday tenantToday, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid organisationId, Guid identifierId, EndOrganisationExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId, reason);

        var (organisation, refusal) = await OrganisationChanges.LoadAsync(context, organisationId, request.Version, ifMatch, cancellationToken);
        if (organisation is null)
        {
            return refusal;
        }

        var identifier = await context.Set<ExternalIdentifier>().SingleOrDefaultAsync(
            i => i.Id == identifierId && i.EntityType == ExternalIdentifierEntityType.Organisation && i.EntityId == organisationId,
            cancellationToken);
        if (identifier is null)
        {
            return new(RegistryErrors.OrganisationExternalIdNotFound);
        }

        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, identifier.Authority));
        var today = await tenantToday.GetAsync(cancellationToken);
        var ended = identifier.End(request.ValidTo ?? today, today);
        if (ended.IsFailure)
        {
            return new(ended.Error, ended.Error.Code == RegistryErrors.OrganisationExternalIdValidityInvalid.Code ? "validTo" : null);
        }

        context.MarkChanged(organisation);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}
