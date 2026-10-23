using FluentValidation;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.CancelSubscription;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class CancelSubscriptionRequestValidator : AbstractValidator<CancelSubscriptionRequest>
{
    public CancelSubscriptionRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= SubscriptionPeriod.CancelReasonMaxLength)
            .WithMessage($"'reason' must be at most {SubscriptionPeriod.CancelReasonMaxLength} characters.");
    }
}
