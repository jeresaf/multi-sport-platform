using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;

/// <summary>
/// Creates an organisation (S07 D4, D9, D13, D21). The details' own rules come first. In a transaction holding
/// <c>registry.organisations:&lt;tenantId&gt;</c>, the slug is checked (or proposed from the display name with
/// <c>-2</c>, <c>-3</c>… on a clash) and the duplicate warning runs: organisations that look like this one refuse the
/// create with <c>ORGANISATION_DUPLICATE_REVIEW_REQUIRED</c> and the candidates, until the request says
/// <c>duplicatesReviewed</c>. The audit entry names the candidates shown and the reason. Two creates of one organisation
/// queue on the lock, and the second finds the first's.
/// </summary>
public sealed class CreateOrganisationHandler(
    RegistryDbContext context,
    OrganisationDetailsRules rules,
    OrganisationDuplicateFinder finder,
    ITenantAccess tenantAccess,
    ITenantContext tenantContext,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    // The candidates of a refused create, in the Problem Details (S07 D13; documented as OrganisationDuplicateReviewProblem).
    public const string CandidatesExtension = "candidates";

    public async Task<(Guid? OrganisationId, ProblemHttpResult? Refusal)> HandleAsync(
        CreateOrganisationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("An organisation is created under the request's tenant.");

        // Described before anything can refuse, so a refused create is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, reason: reason);

        var access = await tenantAccess.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} is unknown.");
        var (details, refusal) = await rules.CheckAsync(request, access.Country, currentPrimaryVenueId: null, cancellationToken);
        if (refusal is not null)
        {
            return (null, refusal.ToProblem());
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await OrganisationChanges.LockOrganisationsAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every organisation committed before it.
        var slug = await SlugAsync(request.Slug, details!.DisplayName, cancellationToken);
        if (slug is null)
        {
            return (null, new FieldRefusal(RegistryErrors.OrganisationSlugTaken, "slug").ToProblem());
        }

        var candidates = await finder.FindAsync(InputOf(details, organisationId: null), cancellationToken);
        audit.Describe(metadata: new OrganisationDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new OrganisationDuplicateCandidateAudit(candidate.OrganisationId, candidate.Confidence))]));
        if (candidates.Count > 0 && !request.DuplicatesReviewed)
        {
            var problem = new FieldRefusal(RegistryErrors.OrganisationDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
            problem.ProblemDetails.Extensions[CandidatesExtension] =
                candidates.Select(OrganisationDuplicateCandidateResponse.From).ToList();
            return (null, problem);
        }

        var organisation = Organisation.Create(details, slug, OrganisationSource.Manual, actor, timeProvider.GetUtcNow());
        context.Add(organisation);
        audit.Describe(entityId: organisation.Id);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OrganisationConfiguration.SlugIndex,
        })
        {
            // Not reachable while every write of a slug holds the lock; kept so a missed lock can't answer 500.
            return (null, new FieldRefusal(RegistryErrors.OrganisationSlugTaken, "slug").ToProblem());
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return (organisation.Id, null);
    }

    /// <summary>What the duplicate warning compares for <paramref name="details"/>.</summary>
    public static OrganisationDuplicateInput InputOf(OrganisationDetails details, Guid? organisationId)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new(
            Organisation.NameKeyOf(details.DisplayName),
            Organisation.OptionalNameKeyOf(details.LegalName),
            Organisation.OptionalNameKeyOf(details.ShortName),
            details.Type,
            Organisation.CityKeyOf(details.City),
            organisationId);
    }

    // The slug sent when it is free (null when taken), or the first free one proposed from the display name.
    private async Task<string?> SlugAsync(string? sent, string displayName, CancellationToken cancellationToken)
    {
        if (sent is not null)
        {
            return await context.Set<Organisation>().AnyAsync(o => o.Slug == sent, cancellationToken) ? null : sent;
        }

        var proposal = Slugs.Propose("organisation", displayName);
        var prefix = proposal + "-";
        var taken = (await context.Set<Organisation>().AsNoTracking()
                .Where(o => o.Slug == proposal || o.Slug.StartsWith(prefix))
                .Select(o => o.Slug)
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
