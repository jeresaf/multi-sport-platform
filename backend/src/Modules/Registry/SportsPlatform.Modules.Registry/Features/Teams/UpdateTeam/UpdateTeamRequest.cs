using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.UpdateTeam;

/// <summary>
/// A team's editable fields, replaced whole (S07 D11): a field left out is cleared, <c>level</c> left out is
/// <c>FIRST</c>, <c>slug</c> left out keeps the team's (D9). The variant can't be sent (it never changes). <c>name</c> may
/// be sent so a form can post the whole team, but only as the current name: another one is
/// <c>TEAM_RENAME_REQUIRED</c> (D8; owner, T3b-1 plan). After the duplicate warning (a change to the short name, the
/// category or the organisation, D13), <c>duplicatesReviewed</c> with an optional reason. <c>version</c>, when sent,
/// must be the team's current one (or <c>If-Match</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTeamRequest(
    GenderCategory? GenderCategory,
    AgeGroup? AgeGroup,
    TeamLevel? Level = null,
    string? Name = null,
    string? ShortName = null,
    Guid? OrganisationId = null,
    string? Code = null,
    string? Motto = null,
    string? PrimaryColour = null,
    string? SecondaryColour = null,
    string? TertiaryColour = null,
    Guid? HomeVenueId = null,
    int? FoundedYear = null,
    string? Slug = null,
    bool DuplicatesReviewed = false,
    string? DuplicateReason = null,
    uint? Version = null) : ITeamChangeRequest;

/// <summary>
/// The request's shape: <see cref="TeamChangeValidator{T}"/>, and a name, when sent, not empty and within a create's
/// length (whether it is the team's is the handler's).
/// </summary>
public sealed class UpdateTeamRequestValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamRequestValidator()
    {
        Include(new TeamChangeValidator<UpdateTeamRequest>());
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => name is null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("'name' must not be empty when sent.")
            .Must(name => name is null || name.Trim().Length <= TeamDetails.NameMaxLength)
            .WithMessage($"'name' must be at most {TeamDetails.NameMaxLength} characters.");
    }
}
