using FluentValidation;
using SportsPlatform.Modules.Identity.Features.Account.Login;

namespace SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;

/// <summary>Shape only: whether the email has an account is never answered.</summary>
public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() =>
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("'email' is required.")
            .MaximumLength(LoginRequestValidator.EmailMaxLength)
            .WithMessage($"'email' must be at most {LoginRequestValidator.EmailMaxLength} characters.");
}
