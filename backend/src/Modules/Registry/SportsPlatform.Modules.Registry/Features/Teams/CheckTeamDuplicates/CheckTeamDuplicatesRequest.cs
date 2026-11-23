using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams.CheckTeamDuplicates;

/// <summary>What the team duplicate warning compares (S07 D13): the names, variant, category and organisation a create would send.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckTeamDuplicatesRequest(
    string? Variant,
    string? Name,
    GenderCategory? GenderCategory,
    AgeGroup? AgeGroup,
    TeamLevel? Level = null,
    string? ShortName = null,
    Guid? OrganisationId = null);

/// <summary>The teams that look like the one checked, strongest first (none is an empty list).</summary>
public sealed record CheckTeamDuplicatesResponse(IReadOnlyList<TeamDuplicateCandidateResponse> Candidates);

/// <summary>The check's shape: the variant, names and category as on a team.</summary>
public sealed class CheckTeamDuplicatesRequestValidator : AbstractValidator<CheckTeamDuplicatesRequest>
{
    public CheckTeamDuplicatesRequestValidator()
    {
        RuleFor(request => request.Variant)
            .Cascade(CascadeMode.Stop)
            .Must(variant => !string.IsNullOrWhiteSpace(variant))
            .WithMessage("'variant' is required.")
            .Must(variant => variant!.Length <= TeamDetails.VariantMaxLength)
            .WithMessage($"'variant' must be at most {TeamDetails.VariantMaxLength} characters.");
        TeamText.Rule(RuleFor(request => request.Name), "name", TeamDetails.NameMaxLength, required: true);
        TeamText.Rule(RuleFor(request => request.ShortName), "shortName", TeamDetails.ShortNameMaxLength, required: false);
        RuleFor(request => request.GenderCategory).NotNull().WithMessage("'genderCategory' is required.");
        RuleFor(request => request.AgeGroup).NotNull().WithMessage("'ageGroup' is required.");
    }
}
