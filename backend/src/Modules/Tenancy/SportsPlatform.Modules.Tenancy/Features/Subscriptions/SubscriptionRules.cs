using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Tenancy.Domain.Plans;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions;

/// <summary>Rules the subscription requests share. Messages name the field and what is accepted, never the value.</summary>
internal static class SubscriptionRules
{
    /// <summary>An active plan's code; an unknown or inactive one is <c>TENANT_PLAN_NOT_FOUND</c> on the field.</summary>
    public static IRuleBuilderOptions<T, string?> ActivePlan<T>(this IRuleBuilderInitial<T, string?> rule, TenancyDbContext context) => rule
        .Cascade(CascadeMode.Stop)
        .Must(code => code is { Length: >= 1 and <= SubscriptionPeriod.PlanCodeMaxLength })
        .WithMessage("'planCode' must be a plan code, such as STANDARD.")
        .MustAsync((code, cancellationToken) => context.Set<Plan>().AnyAsync(p => p.Code == code && p.IsActive, cancellationToken))
        .WithMessage("'planCode' must be an active plan (GET /api/platform/v1/plans).")
        .WithErrorCode(TenancyErrors.PlanNotFound.Code);

    public static IRuleBuilderOptions<T, int?> WindowDays<T>(this IRuleBuilder<T, int?> rule, string field) => rule
        .InclusiveBetween(0, SubscriptionPeriod.MaxWindowDays)
        .WithMessage($"'{field}' must be 0–{SubscriptionPeriod.MaxWindowDays} days.");
}
