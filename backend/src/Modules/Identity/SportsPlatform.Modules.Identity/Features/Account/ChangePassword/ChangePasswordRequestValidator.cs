using FluentValidation;
using SportsPlatform.Modules.Identity.Features.Account.Login;

namespace SportsPlatform.Modules.Identity.Features.Account.ChangePassword;

/// <summary>Shape only: whether the current password is right and the new one passes the policy is the handler's.</summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty().WithMessage("'currentPassword' is required.")
            .MaximumLength(LoginRequestValidator.PasswordMaxLength)
            .WithMessage($"'currentPassword' must be at most {LoginRequestValidator.PasswordMaxLength} characters.");
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage("'newPassword' is required.")
            .MaximumLength(LoginRequestValidator.PasswordMaxLength)
            .WithMessage($"'newPassword' must be at most {LoginRequestValidator.PasswordMaxLength} characters.");
    }
}
