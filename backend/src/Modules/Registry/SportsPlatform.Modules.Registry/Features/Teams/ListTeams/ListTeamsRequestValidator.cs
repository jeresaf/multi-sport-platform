using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People.Profiles;

namespace SportsPlatform.Modules.Registry.Features.Teams.ListTeams;

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class ListTeamsRequestValidator : AbstractValidator<ListTeamsRequest>
{
    public ListTeamsRequestValidator()
    {
        RuleFor(request => request.Q)
            .Must(q => q is null || q.Length <= ListTeamsRequest.SearchMaxLength)
            .WithMessage($"'q' is at most {ListTeamsRequest.SearchMaxLength} characters.")
            // PostgreSQL refuses a NUL in text, and no name holds a control character.
            .Must(q => q is null || !q.Any(char.IsControl))
            .WithMessage("'q' can't contain control characters.");
        RuleFor(request => request.Variant)
            .Must(variant => variant is null || AthleteProfileDetailsRules.IsCode(variant, TeamDetails.VariantMaxLength))
            .WithMessage($"'variant' must be a sport variant code of at most {TeamDetails.VariantMaxLength} characters.");
        RuleFor(request => request.OrganisationId)
            .Must(id => id is null || ListTeamsRequest.TryParseOrganisationId(id, out _))
            .WithMessage("'organisationId' must be an organisation ID (a UUID).");
        RuleFor(request => request.HomeVenueId)
            .Must(id => id is null || ListTeamsRequest.TryParseHomeVenueId(id, out _))
            .WithMessage("'homeVenueId' must be a venue ID (a UUID).");
        RuleFor(request => request.GenderCategory)
            .Must(gender => gender is null || ListTeamsRequest.TryParseGenderCategory(gender, out _))
            .WithMessage($"'genderCategory' must be one of: {string.Join(", ", ListTeamsRequest.GenderCategories)}.");
        RuleFor(request => request.AgeGroup)
            .Must(ageGroup => ageGroup is null || ListTeamsRequest.TryParseAgeGroup(ageGroup, out _))
            .WithMessage($"'ageGroup' must be one of: {string.Join(", ", ListTeamsRequest.AgeGroups)}.");
        RuleFor(request => request.Level)
            .Must(level => level is null || ListTeamsRequest.TryParseLevel(level, out _))
            .WithMessage($"'level' must be one of: {string.Join(", ", ListTeamsRequest.Levels)}.");
        RuleFor(request => request.Status)
            .Must(status => status is null || ListTeamsRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListTeamsRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListTeamsRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListTeamsRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || TeamDirectoryCursor.TryDecode(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
