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

namespace SportsPlatform.Modules.Registry.Features.Venues.ExternalIdentifiers;

/// <summary>
/// Adds an external identifier to a venue (S07 D10): refused on an archived or closed venue. Holding
/// <c>registry.venues:&lt;tenantId&gt;</c> (D21), it runs the exact check: a value with this authority that any venue
/// has or had, this one included, is <c>VENUE_EXTERNAL_ID_EXISTS</c>; people's identifiers are another entity type and
/// don't clash. The national-document authorities are a person rule only. The identifier has no foreign key to the
/// venue, so the venue is marked changed (S06 D11).
/// </summary>
public sealed class AddVenueExternalIdentifierHandler(RegistryDbContext context, ITenantContext tenantContext, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid venueId, AddVenueExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A venue is changed under the request's tenant.");
        var authority = request.Authority!;
        var value = request.Value!.Trim();
        audit.Describe(VenuesEndpoints.EntityType, venueId);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var (venue, refusal) = await VenueChanges.LoadAsync(context, venueId, request.Version, ifMatch, cancellationToken);
        if (venue is null)
        {
            return refusal;
        }

        var accepts = venue.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        await VenueChanges.LockVenuesAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every identifier committed before it.
        if (await context.Set<ExternalIdentifier>().AnyAsync(
                identifier => identifier.EntityType == ExternalIdentifierEntityType.Venue
                    && identifier.Authority == authority && identifier.Value == value,
                cancellationToken))
        {
            return new(RegistryErrors.VenueExternalIdExists, "value");
        }

        var identifier = ExternalIdentifier.ForVenue(venue.Id, authority, value, request.ValidFrom);
        context.Add(identifier);
        context.MarkChanged(venue);
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
            // Not reachable while every write of a venue identifier holds the lock; kept so a missed lock can't answer 500.
            return new(RegistryErrors.VenueExternalIdExists, "value");
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }
}

/// <summary>
/// Ends a venue's external identifier (S07 D10), whatever the venue's status: <c>valid_to</c> is set, the row stays and
/// the value is never reused. The date defaults to the tenant's today and can't be after it or before the identifier's
/// start.
/// </summary>
public sealed class EndVenueExternalIdentifierHandler(RegistryDbContext context, TenantToday tenantToday, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid venueId, Guid identifierId, EndVenueExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        audit.Describe(VenuesEndpoints.EntityType, venueId, reason);

        var (venue, refusal) = await VenueChanges.LoadAsync(context, venueId, request.Version, ifMatch, cancellationToken);
        if (venue is null)
        {
            return refusal;
        }

        var identifier = await context.Set<ExternalIdentifier>().SingleOrDefaultAsync(
            i => i.Id == identifierId && i.EntityType == ExternalIdentifierEntityType.Venue && i.EntityId == venueId,
            cancellationToken);
        if (identifier is null)
        {
            return new(RegistryErrors.VenueExternalIdNotFound);
        }

        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, identifier.Authority));
        var today = await tenantToday.GetAsync(cancellationToken);
        var ended = identifier.End(request.ValidTo ?? today, today);
        if (ended.IsFailure)
        {
            return new(ended.Error, ended.Error.Code == RegistryErrors.VenueExternalIdValidityInvalid.Code ? "validTo" : null);
        }

        context.MarkChanged(venue);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}
