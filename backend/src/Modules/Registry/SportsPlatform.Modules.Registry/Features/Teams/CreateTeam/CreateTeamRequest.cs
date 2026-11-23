using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;

/// <summary>
/// A new team (S07 D6, D7, D8): its variant (enabled for the tenant), name, category and fields, when its name began
/// (<c>nameSince</c>, the tenant's today when left out), an optional slug (proposed from the name and category when none
/// is sent, D9), and, after the duplicate warning, <c>duplicatesReviewed</c> with an optional reason (D13).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateTeamRequest(
    string? Variant,
    string? Name,
    GenderCategory? GenderCategory,
    AgeGroup? AgeGroup,
    TeamLevel? Level = null,
    string? ShortName = null,
    Guid? OrganisationId = null,
    string? Code = null,
    string? Motto = null,
    string? PrimaryColour = null,
    string? SecondaryColour = null,
    string? TertiaryColour = null,
    Guid? HomeVenueId = null,
    int? FoundedYear = null,
    DateOnly? NameSince = null,
    string? Slug = null,
    bool DuplicatesReviewed = false,
    string? DuplicateReason = null) : ITeamChangeRequest;

/// <summary>
/// The request's shape: a variant code, a name, <see cref="TeamChangeValidator{T}"/>, and a <c>nameSince</c> not before
/// 1800. Whether the variant is enabled and <c>nameSince</c> against the tenant's today are the handler's.
/// </summary>
public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        Include(new TeamChangeValidator<CreateTeamRequest>());
        RuleFor(request => request.Variant)
            .Cascade(CascadeMode.Stop)
            .Must(variant => !string.IsNullOrWhiteSpace(variant))
            .WithMessage("'variant' is required.")
            .Must(variant => variant!.Length <= TeamDetails.VariantMaxLength)
            .WithMessage($"'variant' must be at most {TeamDetails.VariantMaxLength} characters.");
        TeamText.Rule(RuleFor(request => request.Name), "name", TeamDetails.NameMaxLength, required: true);
        RuleFor(request => request.NameSince)
            .GreaterThanOrEqualTo(TeamDetails.MinNameSince)
            .WithMessage($"'nameSince' must be {TeamDetails.MinNameSince:yyyy-MM-dd} or later.");
    }
}
