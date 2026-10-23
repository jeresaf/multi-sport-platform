using FluentValidation;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.AssignPeriod;

/// <summary>Scoped: the plan check reads <c>tenancy.plans</c>.</summary>
public sealed class AssignPeriodRequestValidator : AbstractValidator<AssignPeriodRequest>
{
    public AssignPeriodRequestValidator(TenancyDbContext context)
    {
        RuleFor(request => request.PlanCode).ActivePlan(context);
        RuleFor(request => request.Kind).NotNull().WithMessage("'kind' must be PAID, TRIAL or COMPLIMENTARY.");
        RuleFor(request => request.StartsOn).NotNull().WithMessage("'startsOn' must be a date, such as 2026-11-01.");
        RuleFor(request => request.EndsOn)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("'endsOn' must be a date, such as 2027-10-31.")
            .Must((request, endsOn) => request.StartsOn is not { } startsOn || endsOn >= startsOn)
            .WithMessage("'endsOn' must be on or after 'startsOn'.");
        RuleFor(request => request.ExpiringWindowDays).WindowDays("expiringWindowDays");
        RuleFor(request => request.GraceDays).WindowDays("graceDays");
    }
}
