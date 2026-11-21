using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues.Aliases;

/// <summary>
/// A new alias of a venue (S07 D10): the name the venue is known by and its kind; <c>version</c>, when sent, must be the
/// venue's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddVenueAliasRequest(string? Name, AliasKind? Kind, uint? Version = null);

/// <summary>
/// The removal of a venue's alias (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the alias and
/// in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveVenueAliasRequest(string? Reason, uint? Version = null);

/// <summary>A name of 1–120 characters after trimming (a venue name's length), without control characters, and a kind.</summary>
public sealed class AddVenueAliasRequestValidator : AbstractValidator<AddVenueAliasRequest>
{
    public AddVenueAliasRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("'name' must not be empty.")
            .Must(name => name!.Trim().Length <= VenueAlias.NameMaxLength)
            .WithMessage($"'name' must be at most {VenueAlias.NameMaxLength} characters.")
            .Must(name => !name!.Any(char.IsControl))
            .WithMessage("'name' can't contain control characters.");
        RuleFor(request => request.Kind).NotNull().WithMessage("'kind' must not be empty.");
    }
}

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class RemoveVenueAliasRequestValidator : AbstractValidator<RemoveVenueAliasRequest>
{
    public RemoveVenueAliasRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= VenueAlias.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {VenueAlias.ReasonMaxLength} characters.");
    }
}
