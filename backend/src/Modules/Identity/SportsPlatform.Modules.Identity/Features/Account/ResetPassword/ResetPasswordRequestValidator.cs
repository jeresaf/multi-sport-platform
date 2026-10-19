using FluentValidation;
using SportsPlatform.Modules.Identity.Features.Account.Login;

namespace SportsPlatform.Modules.Identity.Features.Account.ResetPassword;

/// <summary>Shape only: whether the token works and the password passes the policy is the handler's.</summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>A real token is 76 characters (<c>{userId:N}.{secret}</c>).</summary>
    public const int TokenMaxLength = 128;

    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("'token' is required.")
            .MaximumLength(TokenMaxLength).WithMessage($"'token' must be at most {TokenMaxLength} characters.");
        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage("'newPassword' is required.")
            .MaximumLength(LoginRequestValidator.PasswordMaxLength)
            .WithMessage($"'newPassword' must be at most {LoginRequestValidator.PasswordMaxLength} characters.");
    }
}
