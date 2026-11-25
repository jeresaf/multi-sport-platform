using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.Organisations.Logo;

/// <summary>
/// Sets an organisation's logo (S07 D15): refused on a dissolved or archived organisation (D10) and at the
/// <c>STORAGE_GB</c> limit (checked before storing, as a person's photo, S06 D20). Platform checks the file and stores it
/// as a <c>PUBLIC</c> <c>ORG_LOGO</c>; the organisation then names it, and only after that save is the old asset marked
/// <c>REMOVED</c>.
/// </summary>
public sealed class SetOrganisationLogoHandler(
    RegistryDbContext context,
    IMediaAssets media,
    IEntitlements entitlements,
    AuditScope audit,
    ILogger<SetOrganisationLogoHandler> logger)
{
    public async Task<FieldRefusal?> HandleAsync(Guid organisationId, Stream file, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId);

        var (organisation, refusal) = await OrganisationChanges.LoadAsync(context, organisationId, version: null, ifMatch, cancellationToken);
        if (organisation is null)
        {
            return refusal;
        }

        var accepts = organisation.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        if (await RegistryMedia.CheckStorageAsync(media, entitlements, cancellationToken) is { } overQuota)
        {
            return overQuota;
        }

        var stored = await media.StoreAsync(file, MediaPurpose.OrgLogo, cancellationToken);
        if (stored.IsFailure)
        {
            return new(stored.Error, RegistryMedia.FileField);
        }

        var set = organisation.SetLogo(stored.Value);
        if (set.IsFailure)
        {
            return new(set.Error);
        }

        audit.Describe(metadata: new RegistryImageAuditMetadata(stored.Value, set.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        if (set.Value is { } previous)
        {
            await RegistryMedia.RemoveUnreferencedAsync(media, logger, previous);
        }

        return null;
    }
}

/// <summary>
/// Removes an organisation's logo in any status (S07 D10): <c>ORGANISATION_LOGO_NOT_FOUND</c> without one. The asset is
/// marked <c>REMOVED</c> after the organisation's save.
/// </summary>
public sealed class RemoveOrganisationLogoHandler(
    RegistryDbContext context, IMediaAssets media, AuditScope audit, ILogger<RemoveOrganisationLogoHandler> logger)
{
    public async Task<FieldRefusal?> HandleAsync(Guid organisationId, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        audit.Describe(OrganisationsEndpoints.EntityType, organisationId);

        var (organisation, refusal) = await OrganisationChanges.LoadAsync(context, organisationId, version: null, ifMatch, cancellationToken);
        if (organisation is null)
        {
            return refusal;
        }

        var removed = organisation.RemoveLogo();
        if (removed.IsFailure)
        {
            return new(removed.Error);
        }

        audit.Describe(metadata: new RegistryImageAuditMetadata(null, removed.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        await RegistryMedia.RemoveUnreferencedAsync(media, logger, removed.Value);
        return null;
    }
}
