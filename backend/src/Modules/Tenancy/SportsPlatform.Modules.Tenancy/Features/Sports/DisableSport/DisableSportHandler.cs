using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Sports;

namespace SportsPlatform.Modules.Tenancy.Features.Sports.DisableSport;

/// <summary>
/// <c>tenant.sport.disable</c> (S05 D12): closes the variant's enablement now, with the reason. Nothing that uses the
/// variant changes. <c>TENANT_SPORT_NOT_ENABLED</c> when it isn't enabled.
/// </summary>
public sealed class DisableSportHandler(TenantSportChanges changes, ICurrentActor actor, TimeProvider timeProvider)
{
    public Task<Result> HandleAsync(string variant, DisableSportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        return changes.RunAsync(
            variant,
            (sports, target, _) => Task.FromResult<Result<TenantSport>>(sports.Disable(target.Variant, reason, actor, timeProvider.GetUtcNow())),
            reason,
            cancellationToken);
    }
}
