using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Identity.Features.Account.Login;
using SportsPlatform.Modules.Identity.Features.Invitations.Acceptance;

namespace SportsPlatform.Modules.Identity.Features.Invitations.AcceptInvitation;

/// <summary>
/// The token from an invitation email's link, and the password to set when the invited email has no account yet
/// (ignored when it has one: the invitee is signed in as that user instead).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptInvitationRequest(
    [property: PersonalData(PersonalDataClass.Sensitive)] string? Token,
    [property: PersonalData(PersonalDataClass.Sensitive)] string? Password = null);

/// <summary>Shape only: whether a password is needed, and passes the policy, is the handler's.</summary>
public sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(request => request.Token).InvitationToken();
        RuleFor(request => request.Password)
            .MaximumLength(LoginRequestValidator.PasswordMaxLength)
            .WithMessage($"'password' must be at most {LoginRequestValidator.PasswordMaxLength} characters.");
    }
}

/// <summary>The membership the invitee now holds; they sign in to use it (accepting doesn't sign in).</summary>
public sealed record AcceptInvitationResponse(Guid TenantId, Guid MembershipId);
