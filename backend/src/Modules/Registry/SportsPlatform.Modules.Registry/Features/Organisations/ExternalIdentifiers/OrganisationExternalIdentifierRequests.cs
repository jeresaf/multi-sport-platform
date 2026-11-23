using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.People;

namespace SportsPlatform.Modules.Registry.Features.Organisations.ExternalIdentifiers;

/// <summary>
/// A new external identifier of an organisation (S07 D10): the authority's code, its value, and since when it is valid;
/// <c>version</c>, when sent, must be the organisation's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddOrganisationExternalIdentifierRequest(string? Authority, string? Value, DateOnly? ValidFrom = null, uint? Version = null)
    : IIdentifierDetails;

/// <summary>
/// The end of an organisation's external identifier: <c>validTo</c> defaults to the tenant's today. The optional reason is kept
/// in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EndOrganisationExternalIdentifierRequest(DateOnly? ValidTo = null, string? Reason = null, uint? Version = null);

/// <summary>The identifier's shape, as a person's (S07 D10).</summary>
public sealed class AddOrganisationExternalIdentifierRequestValidator : AbstractValidator<AddOrganisationExternalIdentifierRequest>
{
    public AddOrganisationExternalIdentifierRequestValidator() => IdentifierDetailsRules.Apply(this);
}

/// <summary>A reason, when sent, of 1–500 characters after trimming.</summary>
public sealed class EndOrganisationExternalIdentifierRequestValidator : AbstractValidator<EndOrganisationExternalIdentifierRequest>
{
    public const int ReasonMaxLength = OrganisationStatusChange.ReasonMaxLength;

    public EndOrganisationExternalIdentifierRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= ReasonMaxLength)
            .WithMessage($"'reason' must be at most {ReasonMaxLength} characters.");
    }
}
