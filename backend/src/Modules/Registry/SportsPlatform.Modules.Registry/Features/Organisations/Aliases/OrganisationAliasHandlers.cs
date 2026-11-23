using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations.Aliases;

/// <summary>
/// Adds an alias to an organisation (S07 D10): refused on a dissolved or archived organisation and when the organisation
/// is already known by the same name key (<c>ORGANISATION_ALIAS_EXISTS</c>). No duplicate warning runs (D13 runs it on
/// create, update and rename only; owner, S07-T2d plan); later warnings compare the alias. The new alias row moves the
/// organisation's version (S06 D11).
/// </summary>
public sealed class AddOrganisationAliasHandler(RegistryDbContext context, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid organisationId, AddOrganisationAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId);
        if (Organisation.NameKeyOf(request.Name).Length == 0)
        {
            return new(RegistryErrors.OrganisationNameInvalid, "name");
        }

        var (organisation, refusal) = await OrganisationChanges.LoadAsync(context, organisationId, request.Version, ifMatch, cancellationToken);
        if (organisation is null)
        {
            return refusal;
        }

        var added = organisation.AddAlias(request.Name!, request.Kind!.Value);
        if (added.IsFailure)
        {
            return new(added.Error, added.Error.Code == RegistryErrors.OrganisationAliasExists.Code ? "name" : null);
        }

        audit.Describe(metadata: new OrganisationAliasAuditMetadata(added.Value.Id));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>Removes an organisation's alias with a reason, in any status (S07 D10); the alias stays with who, when and why.</summary>
public sealed class RemoveOrganisationAliasHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid organisationId, Guid aliasId, RemoveOrganisationAliasRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId, reason, new OrganisationAliasAuditMetadata(aliasId));

        var (organisation, refusal) = await OrganisationChanges.LoadAsync(context, organisationId, request.Version, ifMatch, cancellationToken);
        if (organisation is null)
        {
            return refusal;
        }

        var removed = organisation.RemoveAlias(aliasId, reason, actor, timeProvider.GetUtcNow());
        if (removed.IsFailure)
        {
            return new(removed.Error);
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>The audit metadata of an organisation alias change: the alias by ID.</summary>
public sealed record OrganisationAliasAuditMetadata([property: PersonalData(PersonalDataClass.Internal)] Guid AliasId);
