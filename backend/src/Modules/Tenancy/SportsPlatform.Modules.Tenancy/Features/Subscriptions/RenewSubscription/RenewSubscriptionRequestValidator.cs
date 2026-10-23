using FluentValidation;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.RenewSubscription;

/// <summary>Scoped: the plan check reads <c>tenancy.plans</c>.</summary>
public sealed class RenewSubscriptionRequestValidator : AbstractValidator<RenewSubscriptionRequest>
{
    public RenewSubscriptionRequestValidator(TenancyDbContext context)
    {
        RuleFor(request => request.EndsOn).NotNull().WithMessage("'endsOn' must be a date, such as 2027-10-31.");
        RuleFor(request => request.PlanCode).ActivePlan(context).When(request => request.PlanCode is not null);
        RuleFor(request => request.ExpiringWindowDays).WindowDays("expiringWindowDays");
        RuleFor(request => request.GraceDays).WindowDays("graceDays");
    }
}
