using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;

/// <summary>
/// Creates a venue (S07 D3, D9, D13, D21). The details' own rules come first. In a transaction holding
/// <c>registry.venues:&lt;tenantId&gt;</c>, the slug is checked (or proposed from the name with <c>-2</c>, <c>-3</c>…
/// on a clash) and the duplicate warning runs: venues that look like this one refuse the create with
/// <c>VENUE_DUPLICATE_REVIEW_REQUIRED</c> and the candidates, until the request says <c>duplicatesReviewed</c>. The
/// audit entry names the candidates shown and the reason. Two creates of one venue queue on the lock, and the second
/// finds the first's venue.
/// </summary>
public sealed class CreateVenueHandler(
    RegistryDbContext context,
    VenueDetailsRules rules,
    VenueDuplicateFinder finder,
    ITenantAccess tenantAccess,
    ITenantContext tenantContext,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    // The candidates of a refused create, in the Problem Details (S07 D13; documented as VenueDuplicateReviewProblem).
    public const string CandidatesExtension = "candidates";

    public async Task<(Guid? VenueId, ProblemHttpResult? Refusal)> HandleAsync(CreateVenueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A venue is created under the request's tenant.");

        // Described before anything can refuse, so a refused create is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(VenuesEndpoints.EntityType, reason: reason);

        var access = await tenantAccess.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} is unknown.");
        var (details, refusal) = await rules.CheckAsync(request, access.Country, cancellationToken);
        if (refusal is not null)
        {
            return (null, refusal.ToProblem());
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await VenueChanges.LockVenuesAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every venue committed before it.
        var slug = await SlugAsync(request.Slug, details!.Name, cancellationToken);
        if (slug is null)
        {
            return (null, new FieldRefusal(RegistryErrors.VenueSlugTaken, "slug").ToProblem());
        }

        var candidates = await finder.FindAsync(
            new VenueDuplicateInput(Venue.NameKeyOf(details.Name), Venue.CityKeyOf(details.City), details.Location, VenueId: null),
            cancellationToken);
        audit.Describe(metadata: new VenueDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new VenueDuplicateCandidateAudit(candidate.VenueId, candidate.Confidence))]));
        if (candidates.Count > 0 && !request.DuplicatesReviewed)
        {
            var problem = new FieldRefusal(RegistryErrors.VenueDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
            problem.ProblemDetails.Extensions[CandidatesExtension] =
                candidates.Select(VenueDuplicateCandidateResponse.From).ToList();
            return (null, problem);
        }

        var venue = Venue.Create(details, slug, VenueSource.Manual, actor, timeProvider.GetUtcNow());
        context.Add(venue);
        audit.Describe(entityId: venue.Id);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: VenueConfiguration.SlugIndex,
        })
        {
            // Not reachable while every write of a slug holds the lock; kept so a missed lock can't answer 500.
            return (null, new FieldRefusal(RegistryErrors.VenueSlugTaken, "slug").ToProblem());
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return (venue.Id, null);
    }

    // The slug sent when it is free (null when taken), or the first free one proposed from the name.
    private async Task<string?> SlugAsync(string? sent, string name, CancellationToken cancellationToken)
    {
        if (sent is not null)
        {
            return await context.Set<Venue>().AnyAsync(v => v.Slug == sent, cancellationToken) ? null : sent;
        }

        var proposal = Slugs.Propose("venue", name);
        var prefix = proposal + "-";
        var taken = (await context.Set<Venue>().AsNoTracking()
                .Where(v => v.Slug == proposal || v.Slug.StartsWith(prefix))
                .Select(v => v.Slug)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var number = 1;
        while (taken.Contains(Slugs.WithSuffix(proposal, number)))
        {
            number++;
        }

        return Slugs.WithSuffix(proposal, number);
    }
}
