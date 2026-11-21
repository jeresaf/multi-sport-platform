using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Venues.Aliases;

/// <summary>
/// Adds an alias to a venue (S07 D10): refused on an archived or closed venue and when the venue is already known by the
/// same name key (<c>VENUE_ALIAS_EXISTS</c>). No duplicate check runs (the warning compares the venue's own name; owner,
/// S07-T1c plan). The new alias row moves the venue's version (S06 D11).
/// </summary>
public sealed class AddVenueAliasHandler(RegistryDbContext context, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid venueId, AddVenueAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(VenuesEndpoints.EntityType, venueId);
        if (Venue.NameKeyOf(request.Name).Length == 0)
        {
            return new(RegistryErrors.VenueNameInvalid, "name");
        }

        var (venue, refusal) = await VenueChanges.LoadAsync(context, venueId, request.Version, ifMatch, cancellationToken);
        if (venue is null)
        {
            return refusal;
        }

        var added = venue.AddAlias(request.Name!, request.Kind!.Value);
        if (added.IsFailure)
        {
            return new(added.Error, added.Error.Code == RegistryErrors.VenueAliasExists.Code ? "name" : null);
        }

        audit.Describe(metadata: new VenueAliasAuditMetadata(added.Value.Id));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>Removes a venue's alias with a reason, in any status (S07 D10); the alias stays with who, when and why.</summary>
public sealed class RemoveVenueAliasHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid venueId, Guid aliasId, RemoveVenueAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        audit.Describe(VenuesEndpoints.EntityType, venueId, reason, new VenueAliasAuditMetadata(aliasId));

        var (venue, refusal) = await VenueChanges.LoadAsync(context, venueId, request.Version, ifMatch, cancellationToken);
        if (venue is null)
        {
            return refusal;
        }

        var removed = venue.RemoveAlias(aliasId, reason, actor, timeProvider.GetUtcNow());
        if (removed.IsFailure)
        {
            return new(removed.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>The audit metadata of a venue alias change: the alias by ID.</summary>
public sealed record VenueAliasAuditMetadata([property: PersonalData(PersonalDataClass.Internal)] Guid AliasId);
