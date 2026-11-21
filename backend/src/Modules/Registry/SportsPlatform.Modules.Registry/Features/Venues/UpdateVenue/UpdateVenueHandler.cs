using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.Modules.Registry.Features.Venues.UpdateVenue;

/// <summary>
/// Replaces a venue's editable fields (S07 D11), whatever its status (a correction; owner, T1b plan). The details' own
/// rules come first, then the version. A change to the slug or to what the duplicate warning compares (the name key,
/// the city key, the coordinates) holds <c>registry.venues:&lt;tenantId&gt;</c> while the slug is checked and the
/// warning runs again, as on create (D13, D21): other venues that look like the changed one refuse the update with
/// <c>VENUE_DUPLICATE_REVIEW_REQUIRED</c> and the candidates until the request says <c>duplicatesReviewed</c>; the
/// audit entry names them and the reason. Any other change runs no warning.
/// </summary>
public sealed class UpdateVenueHandler(
    RegistryDbContext context,
    VenueDetailsRules rules,
    VenueDuplicateFinder finder,
    ITenantAccess tenantAccess,
    ITenantContext tenantContext,
    AuditScope audit)
{
    /// <summary>Null when saved and committed; otherwise the refusal.</summary>
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid venueId, UpdateVenueRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A venue is changed under the request's tenant.");

        // Described before anything can refuse, so a refused update is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(VenuesEndpoints.EntityType, venueId, reason);

        var access = await tenantAccess.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} is unknown.");
        var (details, refusal) = await rules.CheckAsync(request, access.Country, cancellationToken);
        if (refusal is not null)
        {
            return refusal.ToProblem();
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var venue = await context.Set<Venue>().SingleOrDefaultAsync(v => v.Id == venueId, cancellationToken);
        if (venue is null)
        {
            return RegistryErrors.VenueNotFound.ToProblem();
        }

        if (!RowVersion.IsSatisfied(context.Entry(venue).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return RegistryErrors.VenueConcurrencyConflict.ToProblem();
        }

        var slug = request.Slug ?? venue.Slug;
        var slugChanges = slug != venue.Slug;
        var comparedFieldsChange = venue.ChangesComparedFields(details!);
        if (slugChanges || comparedFieldsChange)
        {
            await VenueChanges.LockVenuesAsync(context, tenantId, cancellationToken);
        }

        // Read after the lock: a new statement, so in READ COMMITTED it sees every venue committed before it.
        if (slugChanges && await context.Set<Venue>().AnyAsync(v => v.Slug == slug && v.Id != venueId, cancellationToken))
        {
            return new FieldRefusal(RegistryErrors.VenueSlugTaken, "slug").ToProblem();
        }

        if (comparedFieldsChange && await ReviewDuplicatesAsync(venue, details!, request.DuplicatesReviewed, cancellationToken) is { } stop)
        {
            return stop;
        }

        venue.Update(details!, slug);
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
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: VenueConfiguration.SlugIndex,
        })
        {
            // Not reachable while every write of a slug holds the lock; kept so a missed lock can't answer 500.
            return new FieldRefusal(RegistryErrors.VenueSlugTaken, "slug").ToProblem();
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }

    // D13's warning for the changed details, the venue itself left out; null to go on.
    private async Task<ProblemHttpResult?> ReviewDuplicatesAsync(
        Venue venue, VenueDetails details, bool reviewed, CancellationToken cancellationToken)
    {
        var candidates = await finder.FindAsync(
            new VenueDuplicateInput(Venue.NameKeyOf(details.Name), Venue.CityKeyOf(details.City), details.Location, venue.Id),
            cancellationToken);
        audit.Describe(metadata: new VenueDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new VenueDuplicateCandidateAudit(candidate.VenueId, candidate.Confidence))]));
        if (candidates.Count == 0 || reviewed)
        {
            return null;
        }

        var problem = new FieldRefusal(RegistryErrors.VenueDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
        problem.ProblemDetails.Extensions[CreateVenueHandler.CandidatesExtension] =
            candidates.Select(VenueDuplicateCandidateResponse.From).ToList();
        return problem;
    }
}
