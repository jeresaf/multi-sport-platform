using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.Modules.Registry.Features.Organisations.UpdateOrganisation;

/// <summary>
/// Replaces an organisation's editable fields (S07 D11), whatever its status (a correction; owner, T2b plan, as
/// venues'). The details' own rules come first (a primary venue is checked only when it changes, D6; untracked), then
/// the version.
/// A change to the slug or to what the duplicate warning compares (a name key, the type, the city key) holds
/// <c>registry.organisations:&lt;tenantId&gt;</c> while the slug is checked and the warning runs again, as on create
/// (D13, D21): other organisations that look like the changed one refuse the update with
/// <c>ORGANISATION_DUPLICATE_REVIEW_REQUIRED</c> and the candidates until the request says <c>duplicatesReviewed</c>; the
/// audit entry names them and the reason. Any other change runs no warning.
/// </summary>
public sealed class UpdateOrganisationHandler(
    RegistryDbContext context,
    OrganisationDetailsRules rules,
    OrganisationDuplicateFinder finder,
    ITenantAccess tenantAccess,
    ITenantContext tenantContext,
    AuditScope audit)
{
    /// <summary>Null when saved and committed; otherwise the refusal.</summary>
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid organisationId, UpdateOrganisationRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("An organisation is changed under the request's tenant.");

        // Described before anything can refuse, so a refused update is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId, reason);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var organisation = await context.Set<Organisation>().SingleOrDefaultAsync(o => o.Id == organisationId, cancellationToken);
        if (organisation is null)
        {
            return RegistryErrors.OrganisationNotFound.ToProblem();
        }

        // The details' rules come before the version, as venues': a kept primary venue isn't checked again (D6).
        var access = await tenantAccess.GetAccessAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} is unknown.");
        var (details, refusal) = await rules.CheckAsync(request, access.Country, organisation.PrimaryVenueId, cancellationToken);
        if (refusal is not null)
        {
            return refusal.ToProblem();
        }

        if (!RowVersion.IsSatisfied(
                context.Entry(organisation).Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return RegistryErrors.OrganisationConcurrencyConflict.ToProblem();
        }

        var slug = request.Slug ?? organisation.Slug;
        var slugChanges = slug != organisation.Slug;
        var comparedFieldsChange = organisation.ChangesComparedFields(details!);
        if (slugChanges || comparedFieldsChange)
        {
            await OrganisationChanges.LockOrganisationsAsync(context, tenantId, cancellationToken);
        }

        // Read after the lock: a new statement, so in READ COMMITTED it sees every organisation committed before it.
        if (slugChanges && await context.Set<Organisation>().AnyAsync(o => o.Slug == slug && o.Id != organisationId, cancellationToken))
        {
            return new FieldRefusal(RegistryErrors.OrganisationSlugTaken, "slug").ToProblem();
        }

        if (comparedFieldsChange
            && await ReviewDuplicatesAsync(organisation.Id, details!, request.DuplicatesReviewed, cancellationToken) is { } stop)
        {
            return stop;
        }

        organisation.Update(details!, slug);
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
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OrganisationConfiguration.SlugIndex,
        })
        {
            // Not reachable while every write of a slug holds the lock; kept so a missed lock can't answer 500.
            return new FieldRefusal(RegistryErrors.OrganisationSlugTaken, "slug").ToProblem();
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }

    // D13's warning for the changed details, the organisation itself left out; null to go on.
    private async Task<ProblemHttpResult?> ReviewDuplicatesAsync(
        Guid organisationId, OrganisationDetails details, bool reviewed, CancellationToken cancellationToken)
    {
        var candidates = await finder.FindAsync(CreateOrganisationHandler.InputOf(details, organisationId), cancellationToken);
        audit.Describe(metadata: new OrganisationDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new OrganisationDuplicateCandidateAudit(candidate.OrganisationId, candidate.Confidence))]));
        if (candidates.Count == 0 || reviewed)
        {
            return null;
        }

        var problem = new FieldRefusal(RegistryErrors.OrganisationDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
        problem.ProblemDetails.Extensions[CreateOrganisationHandler.CandidatesExtension] =
            candidates.Select(OrganisationDuplicateCandidateResponse.From).ToList();
        return problem;
    }
}
