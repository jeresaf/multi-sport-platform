using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary>
/// A team's editable fields as sent (S07 D6, D7), shared by create and (S07-T3b) update. The variant and the name are
/// not here: the variant never changes, and the name changes by rename (D8).
/// </summary>
public interface ITeamDetailsRequest
{
    string? ShortName { get; }

    GenderCategory? GenderCategory { get; }

    AgeGroup? AgeGroup { get; }

    /// <summary><c>FIRST</c> when left out.</summary>
    TeamLevel? Level { get; }

    Guid? OrganisationId { get; }

    string? Code { get; }

    string? Motto { get; }

    string? PrimaryColour { get; }

    string? SecondaryColour { get; }

    string? TertiaryColour { get; }

    Guid? HomeVenueId { get; }

    int? FoundedYear { get; }
}

/// <summary>
/// What a create and an update send beside the fields (S07 D9, D13): an optional slug, and, after the duplicate
/// warning, <c>duplicatesReviewed</c> with an optional reason.
/// </summary>
public interface ITeamChangeRequest : ITeamDetailsRequest
{
    const int DuplicateReasonMaxLength = 500;

    string? Slug { get; }

    bool DuplicatesReviewed { get; }

    string? DuplicateReason { get; }
}

/// <summary>
/// The shape of <see cref="ITeamChangeRequest"/>: <see cref="TeamDetailsValidator{T}"/>, a well-formed slug that isn't a
/// UUID, and a reason that isn't blank and comes only with <c>duplicatesReviewed</c>.
/// </summary>
public sealed class TeamChangeValidator<T> : AbstractValidator<T>
    where T : ITeamChangeRequest
{
    public TeamChangeValidator()
    {
        Include(new TeamDetailsValidator<T>());
        RuleFor(request => request.Slug)
            .Must(slug => slug is null || Slugs.IsValid(slug))
            .WithMessage(
                $"'slug' must be {Slugs.MinLength}–{Slugs.MaxLength} lower-case letters and digits in words joined by single hyphens, and not a UUID.");
        RuleFor(request => request.DuplicateReason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'duplicateReason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= ITeamChangeRequest.DuplicateReasonMaxLength)
            .WithMessage($"'duplicateReason' must be at most {ITeamChangeRequest.DuplicateReasonMaxLength} characters.");
        RuleFor(request => request.DuplicateReason)
            .Null().When(request => !request.DuplicatesReviewed)
            .WithMessage("'duplicateReason' comes only with 'duplicatesReviewed'.");
    }
}

/// <summary>
/// The shape of <see cref="ITeamDetailsRequest"/> (S07 D6, D7). Messages name the field, never echo what was sent. The
/// code is three upper-case letters; a colour is <c>#RRGGBB</c> (either case). The colours' order, the founded year
/// against the tenant's year, the organisation and the home venue are <see cref="TeamDetailsRules"/>.
/// </summary>
public sealed partial class TeamDetailsValidator<T> : AbstractValidator<T>
    where T : ITeamDetailsRequest
{
    public TeamDetailsValidator()
    {
        TeamText.Rule(RuleFor(request => request.ShortName), "shortName", TeamDetails.ShortNameMaxLength, required: false);
        TeamText.Rule(RuleFor(request => request.Motto), "motto", TeamDetails.MottoMaxLength, required: false);
        RuleFor(request => request.GenderCategory).NotNull().WithMessage("'genderCategory' is required.");
        RuleFor(request => request.AgeGroup).NotNull().WithMessage("'ageGroup' is required.");
        RuleFor(request => request.Code)
            .Must(code => code is null || CodePattern().IsMatch(code))
            .WithMessage("'code' must be three upper-case letters (A–Z).");
        Colour(RuleFor(request => request.PrimaryColour), "primaryColour");
        Colour(RuleFor(request => request.SecondaryColour), "secondaryColour");
        Colour(RuleFor(request => request.TertiaryColour), "tertiaryColour");
        RuleFor(request => request.FoundedYear)
            .GreaterThanOrEqualTo(TeamDetails.MinFoundedYear)
            .WithMessage($"'foundedYear' must be {TeamDetails.MinFoundedYear} or later.");
    }

    private static void Colour(IRuleBuilderInitial<T, string?> rule, string field) => rule
        .Must(colour => colour is null || ColourPattern().IsMatch(colour))
        .WithMessage($"'{field}' must be a colour as #RRGGBB (six hexadecimal digits).");

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColourPattern();
}

/// <summary>The rule on a team's texts: not blank when required, at most so many characters once trimmed.</summary>
internal static class TeamText
{
    public static void Rule<T>(IRuleBuilderInitial<T, string?> rule, string field, int maxLength, bool required) => rule
        .Cascade(CascadeMode.Stop)
        .Must(text => !required || !string.IsNullOrWhiteSpace(text))
        .WithMessage($"'{field}' must not be empty.")
        .Must(text => text is null || text.Trim().Length <= maxLength)
        .WithMessage($"'{field}' must be at most {maxLength} characters.");
}

/// <summary>
/// The rules on team details that need more than the request's shape (S07 D6): a name without a letter or a digit has
/// no name key (<c>TEAM_NAME_INVALID</c>); a secondary colour without a primary, or a tertiary without a secondary, is
/// <c>TEAM_COLOURS_INVALID</c>; a founded year after the tenant's year is refused on its field
/// (<c>VALIDATION_INVALID_REQUEST</c>); the organisation must be the tenant's and not dissolved or archived
/// (<c>TEAM_ORGANISATION_UNAVAILABLE</c>; owner, S07-T3a plan: D6's code, not D10's), the home venue the tenant's and
/// not closed or archived (<c>TEAM_HOME_VENUE_UNAVAILABLE</c>), each checked only when the request sets or changes it.
/// References are read untracked: they are other aggregates (S06 D11), and another tenant's is not found (RLS).
/// </summary>
public sealed class TeamDetailsRules(RegistryDbContext context)
{
    /// <summary>
    /// The details to save, trimmed, with colours in upper case and <c>FIRST</c> when no level was sent; or the first
    /// refusal. <paramref name="current"/> is the team before the change (null on a create): a reference it keeps isn't
    /// checked again. <paramref name="today"/> is the tenant's date, read once by the use case (review #127 R3).
    /// </summary>
    public async Task<(TeamDetails? Details, FieldRefusal? Refusal)> CheckAsync(
        ITeamDetailsRequest request, string? name, Team? current, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Team.NameKeyOf(name).Length == 0)
        {
            return (null, new(RegistryErrors.TeamNameInvalid, "name"));
        }

        var primary = Colour(request.PrimaryColour);
        var secondary = Colour(request.SecondaryColour);
        var tertiary = Colour(request.TertiaryColour);
        if (!TeamDetails.ColoursInOrder(primary, secondary, tertiary))
        {
            return (null, new(RegistryErrors.TeamColoursInvalid, secondary is not null && primary is null ? "secondaryColour" : "tertiaryColour"));
        }

        if (request.FoundedYear is { } year && year > today.Year)
        {
            return (null, new(CommonErrors.InvalidRequest, "foundedYear", "'foundedYear' can't be after this year (the tenant's date)."));
        }

        if (request.OrganisationId is { } organisationId && organisationId != current?.OrganisationId)
        {
            var available = await context.Set<Organisation>().AsNoTracking()
                .AnyAsync(
                    o => o.Id == organisationId && o.Status != OrganisationStatus.Dissolved && o.Status != OrganisationStatus.Archived,
                    cancellationToken);
            if (!available)
            {
                return (null, new(RegistryErrors.TeamOrganisationUnavailable, "organisationId"));
            }
        }

        if (request.HomeVenueId is { } venueId && venueId != current?.HomeVenueId)
        {
            var available = await context.Set<Venue>().AsNoTracking()
                .AnyAsync(v => v.Id == venueId && v.Status != VenueStatus.Closed && v.Status != VenueStatus.Archived, cancellationToken);
            if (!available)
            {
                return (null, new(RegistryErrors.TeamHomeVenueUnavailable, "homeVenueId"));
            }
        }

        return (new TeamDetails(
            name!.Trim(),
            Blank(request.ShortName),
            new TeamCategory(request.GenderCategory!.Value, request.AgeGroup!.Value, request.Level ?? TeamLevel.First),
            request.OrganisationId,
            request.Code,
            Blank(request.Motto),
            primary,
            secondary,
            tertiary,
            request.HomeVenueId,
            request.FoundedYear), null);
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Colour(string? colour) => colour?.ToUpperInvariant();
}
