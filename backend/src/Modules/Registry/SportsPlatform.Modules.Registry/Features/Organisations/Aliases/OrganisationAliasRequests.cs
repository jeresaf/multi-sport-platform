using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.Organisations.Aliases;

/// <summary>
/// A new alias of an organisation (S07 D10): the name the organisation is known by and its kind; <c>version</c>, when sent, must be the
/// organisation's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddOrganisationAliasRequest(string? Name, AliasKind? Kind, uint? Version = null);

/// <summary>
/// The removal of an organisation's alias (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the alias and
/// in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveOrganisationAliasRequest(string? Reason, uint? Version = null);

/// <summary>A name of 1–120 characters after trimming (a display name's length), without control characters, and a kind.</summary>
public sealed class AddOrganisationAliasRequestValidator : AbstractValidator<AddOrganisationAliasRequest>
{
    public AddOrganisationAliasRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("'name' must not be empty.")
            .Must(name => name!.Trim().Length <= OrganisationAlias.NameMaxLength)
            .WithMessage($"'name' must be at most {OrganisationAlias.NameMaxLength} characters.")
            .Must(name => !name!.Any(char.IsControl))
            .WithMessage("'name' can't contain control characters.");
        RuleFor(request => request.Kind).NotNull().WithMessage("'kind' must not be empty.");
    }
}

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class RemoveOrganisationAliasRequestValidator : AbstractValidator<RemoveOrganisationAliasRequest>
{
    public RemoveOrganisationAliasRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= OrganisationAlias.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {OrganisationAlias.ReasonMaxLength} characters.");
    }
}
