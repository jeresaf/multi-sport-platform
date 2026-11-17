using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People.Aliases;

/// <summary>
/// A new alias (S06 D10): the name as the person is known by it and its kind; <c>version</c>, when sent, must be the
/// person's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddPersonAliasRequest(string? Name, AliasKind? Kind, uint? Version = null);

/// <summary>
/// The removal of an alias (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the alias and in the
/// audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemovePersonAliasRequest(string? Reason, uint? Version = null);

/// <summary>A name of 1–200 characters after trimming, without control characters, and a kind.</summary>
public sealed class AddPersonAliasRequestValidator : AbstractValidator<AddPersonAliasRequest>
{
    public AddPersonAliasRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("'name' must not be empty.")
            .Must(name => name!.Trim().Length <= PersonAlias.NameMaxLength)
            .WithMessage($"'name' must be at most {PersonAlias.NameMaxLength} characters.")
            .Must(name => !name!.Any(char.IsControl))
            .WithMessage("'name' can't contain control characters.");
        RuleFor(request => request.Kind).NotNull().WithMessage("'kind' must not be empty.");
    }
}

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class RemovePersonAliasRequestValidator : AbstractValidator<RemovePersonAliasRequest>
{
    public RemovePersonAliasRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= PersonAlias.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {PersonAlias.ReasonMaxLength} characters.");
    }
}
