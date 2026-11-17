using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People.ChangePersonStatus;

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class ChangePersonStatusWithReasonRequestValidator : AbstractValidator<ChangePersonStatusWithReasonRequest>
{
    public ChangePersonStatusWithReasonRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= PersonStatusChange.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {PersonStatusChange.ReasonMaxLength} characters.");
    }
}

/// <summary>A reason, when sent, of 1–500 characters after trimming.</summary>
public sealed class ChangePersonStatusRequestValidator : AbstractValidator<ChangePersonStatusRequest>
{
    public ChangePersonStatusRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= PersonStatusChange.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {PersonStatusChange.ReasonMaxLength} characters.");
    }
}
