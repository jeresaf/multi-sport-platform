using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Features.Media;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.Teams.Images;

/// <summary>A team's two images (S07 D15): each set and removed on its own, with its own purpose.</summary>
public enum TeamImage
{
    Logo,
    Badge,
}

/// <summary>
/// Sets a team's logo or badge (S07 D15): refused on a disbanded or archived team (D10) and at the <c>STORAGE_GB</c> limit
/// (checked before storing, as a person's photo, S06 D20). Platform checks the file and stores it as a <c>PUBLIC</c>
/// <c>TEAM_LOGO</c> or <c>TEAM_BADGE</c>; the team then names it, and only after that save is the old asset marked
/// <c>REMOVED</c>. The other image doesn't change.
/// </summary>
public sealed class SetTeamImageHandler(
    RegistryDbContext context,
    IMediaAssets media,
    IEntitlements entitlements,
    AuditScope audit,
    ILogger<SetTeamImageHandler> logger)
{
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, TeamImage image, Stream file, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        audit.Describe(TeamsEndpoints.EntityType, teamId);

        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, version: null, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        var accepts = team.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error.ToProblem();
        }

        if (await RegistryMedia.CheckStorageAsync(media, entitlements, cancellationToken) is { } overQuota)
        {
            return overQuota.ToProblem();
        }

        var purpose = image == TeamImage.Logo ? MediaPurpose.TeamLogo : MediaPurpose.TeamBadge;
        var stored = await media.StoreAsync(file, purpose, cancellationToken);
        if (stored.IsFailure)
        {
            return new FieldRefusal(stored.Error, RegistryMedia.FileField).ToProblem();
        }

        var set = image == TeamImage.Logo ? team.SetLogo(stored.Value) : team.SetBadge(stored.Value);
        if (set.IsFailure)
        {
            return set.Error.ToProblem();
        }

        audit.Describe(metadata: new RegistryImageAuditMetadata(stored.Value, set.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error.ToProblem();
        }

        if (set.Value is { } previous)
        {
            await RegistryMedia.RemoveUnreferencedAsync(media, logger, previous);
        }

        return null;
    }
}

/// <summary>
/// Removes a team's logo or badge in any status (S07 D10): <c>TEAM_LOGO_NOT_FOUND</c> or <c>TEAM_BADGE_NOT_FOUND</c>
/// without one. The asset is marked <c>REMOVED</c> after the team's save.
/// </summary>
public sealed class RemoveTeamImageHandler(
    RegistryDbContext context, IMediaAssets media, AuditScope audit, ILogger<RemoveTeamImageHandler> logger)
{
    public async Task<ProblemHttpResult?> HandleAsync(Guid teamId, TeamImage image, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        audit.Describe(TeamsEndpoints.EntityType, teamId);

        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, version: null, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        Result<Guid> removed = image == TeamImage.Logo ? team.RemoveLogo() : team.RemoveBadge();
        if (removed.IsFailure)
        {
            return removed.Error.ToProblem();
        }

        audit.Describe(metadata: new RegistryImageAuditMetadata(null, removed.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error.ToProblem();
        }

        await RegistryMedia.RemoveUnreferencedAsync(media, logger, removed.Value);
        return null;
    }
}
