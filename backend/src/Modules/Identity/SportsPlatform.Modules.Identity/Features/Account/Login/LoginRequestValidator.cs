using FluentValidation;

namespace SportsPlatform.Modules.Identity.Features.Account.Login;

/// <summary>Shape only: whether the email and password are right is the handler's, and the answer is the same either way.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Longer passwords are refused before they are hashed.</summary>
    public const int PasswordMaxLength = 512;

    public const int EmailMaxLength = 256;

    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("'email' is required.")
            .MaximumLength(EmailMaxLength).WithMessage($"'email' must be at most {EmailMaxLength} characters.");
        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("'password' is required.")
            .MaximumLength(PasswordMaxLength).WithMessage($"'password' must be at most {PasswordMaxLength} characters.");
    }
}
