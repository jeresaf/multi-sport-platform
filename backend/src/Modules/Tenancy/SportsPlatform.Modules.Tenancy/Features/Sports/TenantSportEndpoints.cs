using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;
using SportsPlatform.Modules.Tenancy.Features.Sports.DisableSport;
using SportsPlatform.Modules.Tenancy.Features.Sports.EnableSport;
using SportsPlatform.Modules.Tenancy.Features.Sports.ListTenantSports;

namespace SportsPlatform.Modules.Tenancy.Features.Sports;

/// <summary>
/// <c>/api/admin/v1/tenant/sports</c> (S05 D15, D16): the request's tenant's sport variants, and enabling and disabling
/// them. Enable and disable answer with the list as it is after the change.
/// </summary>
internal static class TenantSportEndpoints
{
    public const string Tag = "Tenant sports";

    private const string _variantNote =
        "`variant` is a published variant's code (GET /api/admin/v1/sports), matched exactly; another is " +
        "TENANT_SPORT_VARIANT_UNKNOWN on the field `variant`.";

    public static void Map(IEndpointRouteBuilder admin)
    {
        var sports = admin.MapGroup("/tenant/sports").WithTags(Tag);

        sports.MapGet("", ListAsync)
            .WithName("ListTenantSports")
            .WithSummary("List the sport variants and which the tenant has enabled")
            .WithDescription(
                "Every published sport variant (latest version) with its sport, name and scoring family, and whether the " +
                "tenant named by `X-Tenant-Id` has it enabled (`enabledSince`: when its current enablement began). " +
                "`sportQuota` is the usage of MAX_ENABLED_SPORTS (distinct sports, not variants; `limit` `null` = " +
                "unlimited; `warning` from 80 % of the limit) for those who may read the subscription, else `null`. " +
                "Available while the tenant is read-only.")
            .RequireAction(TenancyActions.TenantSportRead);

        sports.MapPost("/{variant}/enable", EnableAsync)
            .WithName("EnableTenantSport")
            .WithSummary("Enable a sport variant for the tenant")
            .WithDescription(
                $"{_variantNote} The plan's MAX_ENABLED_SPORTS counts distinct sports: a variant of a sport the tenant " +
                "already has adds nothing and is never refused for it; another sport at the limit is " +
                "ENTITLEMENT_QUOTA_EXCEEDED (\"Your plan allows N sports.\"). An enabled variant is " +
                "TENANT_SPORT_ALREADY_ENABLED. Enabling a variant that was disabled adds a new enablement; the earlier " +
                "ones are kept.")
            .RequireAction(TenancyActions.TenantSportEnable)
            .ProducesErrors(TenancyErrors.SportVariantUnknown, TenancyErrors.SportAlreadyEnabled);

        sports.MapPost("/{variant}/disable", DisableAsync)
            .WithName("DisableTenantSport")
            .WithSummary("Disable a sport variant for the tenant")
            .WithDescription(
                $"{_variantNote} Ends the variant's enablement now, with `reason`; nothing that uses the variant changes, " +
                "and lowering the sport quota never disables a sport. A variant that isn't enabled is " +
                "TENANT_SPORT_NOT_ENABLED. The reason is kept permanently on the enablement and in the audit trail: " +
                "don't include personal data.")
            .RequireAction(TenancyActions.TenantSportDisable)
            .ValidateRequest<DisableSportRequest>(CommonErrors.ReasonRequired)
            .ProducesErrors(TenancyErrors.SportVariantUnknown, TenancyErrors.SportNotEnabled);
    }

    private static async Task<Ok<TenantSportsResponse>> ListAsync(ListTenantSportsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Results<Ok<TenantSportsResponse>, ProblemHttpResult>> EnableAsync(
        string variant,
        EnableSportHandler handler,
        ListTenantSportsHandler list,
        IEntitlements entitlements,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(variant, cancellationToken);
        if (result.IsSuccess)
        {
            return TypedResults.Ok(await list.HandleAsync(CancellationToken.None));
        }

        // A Result carries no detail (S04-T3a2): the limit is read again for the message, as for staff users.
        return result.Error.Code == EntitlementErrorCodes.QuotaExceeded
            ? result.Error.ToProblem($"Your plan allows {await entitlements.GetLimitAsync(EnabledSports.Code, cancellationToken)} sports.")
            : Problem(result.Error);
    }

    private static async Task<Results<Ok<TenantSportsResponse>, ProblemHttpResult>> DisableAsync(
        string variant,
        DisableSportRequest request,
        DisableSportHandler handler,
        ListTenantSportsHandler list,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(variant, request, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(await list.HandleAsync(CancellationToken.None)) : Problem(result.Error);
    }

    // An unknown variant is a field error (D12: 400 on the field), like the validator's.
    private static ProblemHttpResult Problem(Error error) =>
        error == TenancyErrors.SportVariantUnknown
            ? new ValidationResult([new ValidationFailure("variant", "'variant' must be a published sport variant's code.")]).ToProblem(error)
            : error.ToProblem();
}
