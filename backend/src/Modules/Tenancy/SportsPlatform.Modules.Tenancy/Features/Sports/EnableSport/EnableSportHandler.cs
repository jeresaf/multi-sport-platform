using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Features.Sports.EnableSport;

/// <summary>
/// <c>tenant.sport.enable</c> (S05 D12, D13): a new enablement of a published variant. A variant whose sport is already
/// counted adds nothing to <c>MAX_ENABLED_SPORTS</c> and is never refused for it; another sport at the limit is
/// <c>ENTITLEMENT_QUOTA_EXCEEDED</c>. <c>TENANT_SPORT_ALREADY_ENABLED</c> when the variant is enabled.
/// </summary>
public sealed class EnableSportHandler(TenantSportChanges changes, IEntitlements entitlements, ICurrentActor actor, TimeProvider timeProvider)
{
    public Task<Result> HandleAsync(string variant, CancellationToken cancellationToken) =>
        changes.RunAsync(
            variant,
            async (sports, target, sportOf) =>
            {
                if (sports.Find(target.Variant) is null && sports.AddsSport(target.Sport, sportOf))
                {
                    var quota = await entitlements.CheckQuotaAsync(EnabledSports.Code, sports.SportsCounted(sportOf), cancellationToken);
                    if (quota.IsFailure)
                    {
                        return quota.Error;
                    }
                }

                return sports.Enable(target.Variant, actor, timeProvider.GetUtcNow());
            },
            reason: null,
            cancellationToken);
}
