using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People.Profiles;
using SportsPlatform.Modules.Registry.Features.Teams.ListTeams;

namespace SportsPlatform.Modules.Registry.Features.Teams.PublicTeams;

/// <summary>
/// The admin directory's query with one status, <c>ACTIVE</c> when none was sent (S07 D17); never <c>ARCHIVED</c>.
/// </summary>
public sealed record PublicTeamsQuery(TeamsQuery Filters, TeamStatus Status);

/// <summary>
/// The public directory's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>). The
/// rules are the admin directory's, but <c>status</c> takes every status except <c>ARCHIVED</c> (archived teams aren't
/// public; owner, plan 2026-11-25). <c>level</c> is beyond D17's list (owner, plan 2026-11-25).
/// </summary>
public sealed record ListPublicTeamsRequest(
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "variant")] string? Variant,
    [FromQuery(Name = "organisationId")] string? OrganisationId,
    [FromQuery(Name = "genderCategory")] string? GenderCategory,
    [FromQuery(Name = "ageGroup")] string? AgeGroup,
    [FromQuery(Name = "level")] string? Level,
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public static IReadOnlyCollection<string> Statuses { get; } =
        [.. ListTeamsRequest.Statuses.Where(status => TryParseStatus(status, out _))];

    public static bool TryParseStatus(string value, out TeamStatus status) =>
        ListTeamsRequest.TryParseStatus(value, out status) && status != TeamStatus.Archived;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public PublicTeamsQuery ToQuery()
    {
        var status = TeamStatus.Active;
        if (Status is not null && !TryParseStatus(Status, out status))
        {
            throw new InvalidOperationException("Validate the request first.");
        }

        // The admin request parses the shared parameters; the status is checked here.
        var shared = new ListTeamsRequest(Q, Variant, OrganisationId, GenderCategory, AgeGroup, Level, Status: null, Limit, Cursor);
        return new PublicTeamsQuery(shared.ToQuery(), status);
    }
}

/// <summary>The admin directory's rules, but <c>status</c> leaves out <c>ARCHIVED</c>. Messages never echo what was sent.</summary>
public sealed class ListPublicTeamsRequestValidator : AbstractValidator<ListPublicTeamsRequest>
{
    public ListPublicTeamsRequestValidator()
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
            .Must(status => status is null || ListPublicTeamsRequest.TryParseStatus(status, out _))
            .WithMessage($"'status' must be one of: {string.Join(", ", ListPublicTeamsRequest.Statuses)}.");
        RuleFor(request => request.Limit)
            .Must(limit => limit is null || ListTeamsRequest.TryParseLimit(limit, out _))
            .WithMessage($"'limit' must be a whole number between 1 and {ListTeamsRequest.MaxLimit}.");
        RuleFor(request => request.Cursor)
            .Must(cursor => cursor is null || TeamDirectoryCursor.TryDecode(cursor, out _))
            .WithMessage("'cursor' must be a nextCursor returned by this list.");
    }
}
