using FluentValidation;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteOwner;

public sealed class InviteOwnerRequestValidator : AbstractValidator<InviteOwnerRequest>
{
    public InviteOwnerRequestValidator() => RuleFor(request => request.Email).InviteeEmail();
}
