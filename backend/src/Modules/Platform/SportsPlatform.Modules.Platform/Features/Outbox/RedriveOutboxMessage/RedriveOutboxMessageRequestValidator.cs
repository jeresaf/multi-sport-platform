using FluentValidation;

namespace SportsPlatform.Modules.Platform.Features.Outbox.RedriveOutboxMessage;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class RedriveOutboxMessageRequestValidator : AbstractValidator<RedriveOutboxMessageRequest>
{
    public RedriveOutboxMessageRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= RedriveOutboxMessageRequest.MaxReasonLength)
            .WithMessage($"'reason' must be at most {RedriveOutboxMessageRequest.MaxReasonLength} characters.");
    }
}
