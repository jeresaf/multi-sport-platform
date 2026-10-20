using System.ComponentModel.DataAnnotations;
using FluentValidation;
using SportsPlatform.Modules.Identity.Domain.Invitations;

namespace SportsPlatform.Modules.Identity.Features.Invitations;

internal static class InviteeEmailRules
{
    // Identity's own user validator checks the email the same way, so an invitee's email can become a user's.
    private static readonly EmailAddressAttribute _email = new();

    /// <summary>Required, at most 256 characters, an email address; the message never echoes it.</summary>
    public static IRuleBuilderOptions<T, string?> InviteeEmail<T>(this IRuleBuilderInitial<T, string?> rule) => rule
        .Cascade(CascadeMode.Stop)
        .Must(email => !string.IsNullOrWhiteSpace(email))
        .WithMessage("'email' is required.")
        .Must(email => email!.Trim().Length <= Invitation.EmailMaxLength)
        .WithMessage($"'email' must be at most {Invitation.EmailMaxLength} characters.")
        .Must(email => _email.IsValid(email!.Trim()))
        .WithMessage("'email' must be an email address.");
}
