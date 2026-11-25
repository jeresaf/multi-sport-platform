using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.Aliases;

/// <summary>
/// A new nickname or spelling of a team (S07 D8, D10): a former name comes only from a rename. <c>version</c>, when sent,
/// must be the team's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddTeamAliasRequest(string? Name, AliasKind? Kind, uint? Version = null);

/// <summary>
/// The removal of a team's nickname or spelling (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the
/// alias and in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveTeamAliasRequest(string? Reason, uint? Version = null);

/// <summary>A name of 1–120 characters after trimming (a team name's length), without control characters, and <c>NICKNAME</c> or <c>SPELLING</c>.</summary>
public sealed class AddTeamAliasRequestValidator : AbstractValidator<AddTeamAliasRequest>
{
    public AddTeamAliasRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("'name' must not be empty.")
            .Must(name => name!.Trim().Length <= TeamAlias.NameMaxLength)
            .WithMessage($"'name' must be at most {TeamAlias.NameMaxLength} characters.")
            .Must(name => !name!.Any(char.IsControl))
            .WithMessage("'name' can't contain control characters.");
        RuleFor(request => request.Kind)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("'kind' must not be empty.")
            .Must(kind => kind != AliasKind.FormerName)
            .WithMessage("'kind' must be NICKNAME or SPELLING: a former name comes from renaming the team.");
    }
}

/// <summary>A reason of 1–500 characters after trimming.</summary>
public sealed class RemoveTeamAliasRequestValidator : AbstractValidator<RemoveTeamAliasRequest>
{
    public RemoveTeamAliasRequestValidator()
    {
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .Must(reason => reason!.Trim().Length <= TeamAlias.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {TeamAlias.ReasonMaxLength} characters.");
    }
}
