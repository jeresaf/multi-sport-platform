using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Identity.Features.Invitations.Acceptance;

namespace SportsPlatform.Modules.Identity.Features.Invitations.LookupInvitation;

/// <summary>The token from an invitation email's link, in the body so request logs and traces never hold it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LookupInvitationRequest([property: PersonalData(PersonalDataClass.Sensitive)] string? Token);

public sealed class LookupInvitationRequestValidator : AbstractValidator<LookupInvitationRequest>
{
    public LookupInvitationRequestValidator() => RuleFor(request => request.Token).InvitationToken();
}
