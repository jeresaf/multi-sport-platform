using FluentValidation;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.ChangePlan;

/// <summary>Scoped: the plan check reads <c>tenancy.plans</c>.</summary>
public sealed class ChangePlanRequestValidator : AbstractValidator<ChangePlanRequest>
{
    public ChangePlanRequestValidator(TenancyDbContext context) =>
        RuleFor(request => request.PlanCode).ActivePlan(context);
}
