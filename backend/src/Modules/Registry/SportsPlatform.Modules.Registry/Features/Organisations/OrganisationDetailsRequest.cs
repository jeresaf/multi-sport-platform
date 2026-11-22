using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Organisations;

/// <summary>An organisation's editable fields as sent (S07 D4), shared by create and (S07-T2b) update.</summary>
public interface IOrganisationDetailsRequest
{
    OrganisationType? Type { get; }

    string? DisplayName { get; }

    string? LegalName { get; }

    string? ShortName { get; }

    string? Country { get; }

    string? City { get; }

    int? FoundedYear { get; }

    string? Website { get; }

    string? Email { get; }

    string? Phone { get; }

    string? Description { get; }

    Guid? PrimaryVenueId { get; }
}

/// <summary>
/// What a create and an update send beside the fields (S07 D9, D13): an optional slug, and, after the duplicate
/// warning, <c>duplicatesReviewed</c> with an optional reason.
/// </summary>
public interface IOrganisationChangeRequest : IOrganisationDetailsRequest
{
    const int DuplicateReasonMaxLength = 500;

    string? Slug { get; }

    bool DuplicatesReviewed { get; }

    string? DuplicateReason { get; }
}

/// <summary>
/// The shape of <see cref="IOrganisationChangeRequest"/>: <see cref="OrganisationDetailsValidator{T}"/>, a well-formed
/// slug that isn't a UUID, and a reason that isn't blank and comes only with <c>duplicatesReviewed</c>. Scoped: it reads
/// the reference data.
/// </summary>
public sealed class OrganisationChangeValidator<T> : AbstractValidator<T>
    where T : IOrganisationChangeRequest
{
    public OrganisationChangeValidator(IReferenceData reference)
    {
        Include(new OrganisationDetailsValidator<T>(reference));
        RuleFor(request => request.Slug)
            .Must(slug => slug is null || Slugs.IsValid(slug))
            .WithMessage(
                $"'slug' must be {Slugs.MinLength}–{Slugs.MaxLength} lower-case letters and digits in words joined by single hyphens, and not a UUID.");
        RuleFor(request => request.DuplicateReason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'duplicateReason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= IOrganisationChangeRequest.DuplicateReasonMaxLength)
            .WithMessage($"'duplicateReason' must be at most {IOrganisationChangeRequest.DuplicateReasonMaxLength} characters.");
        RuleFor(request => request.DuplicateReason)
            .Null().When(request => !request.DuplicatesReviewed)
            .WithMessage("'duplicateReason' comes only with 'duplicatesReviewed'.");
    }
}

/// <summary>
/// The shape of <see cref="IOrganisationDetailsRequest"/> (S07 D4). Messages name the field, never echo what was sent.
/// The country is an active one of the reference data; the website an absolute <c>https://</c> or <c>http://</c> URL;
/// the email one address; the phone digits with an optional leading <c>+</c>, spaces, hyphens and brackets. Whether the
/// display name has a letter or a digit, the founded year against the tenant's year and the primary venue are
/// <see cref="OrganisationDetailsRules"/>. Scoped: it reads the reference data.
/// </summary>
public sealed class OrganisationDetailsValidator<T> : AbstractValidator<T>
    where T : IOrganisationDetailsRequest
{
    public OrganisationDetailsValidator(IReferenceData reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        RuleFor(request => request.Type).NotNull().WithMessage("'type' is required.");
        Text(RuleFor(request => request.DisplayName), "displayName", OrganisationDetails.DisplayNameMaxLength, required: true);
        Text(RuleFor(request => request.LegalName), "legalName", OrganisationDetails.LegalNameMaxLength, required: false);
        Text(RuleFor(request => request.ShortName), "shortName", OrganisationDetails.ShortNameMaxLength, required: false);
        Text(RuleFor(request => request.City), "city", OrganisationDetails.CityMaxLength, required: false);
        Text(RuleFor(request => request.Description), "description", OrganisationDetails.DescriptionMaxLength, required: false);
        RuleFor(request => request.Country)
            .Cascade(CascadeMode.Stop)
            .Must(code => code is null || (code.Length == 2 && code.All(char.IsAsciiLetterUpper)))
            .WithMessage("'country' must be an ISO 3166-1 alpha-2 country code in upper case.")
            .MustAsync(async (code, cancellationToken) => code is null || await reference.IsActiveCountryAsync(code, cancellationToken))
            .WithMessage("'country' must be a country of the reference data.");
        RuleFor(request => request.FoundedYear)
            .GreaterThanOrEqualTo(OrganisationDetails.MinFoundedYear)
            .WithMessage($"'foundedYear' must be {OrganisationDetails.MinFoundedYear} or later.");
        RuleFor(request => request.Website)
            .Cascade(CascadeMode.Stop)
            .Must(url => url is null || url.Trim().Length <= OrganisationDetails.WebsiteMaxLength)
            .WithMessage($"'website' must be at most {OrganisationDetails.WebsiteMaxLength} characters.")
            .Must(url => url is null || IsWebAddress(url.Trim()))
            .WithMessage("'website' must be an absolute https:// or http:// address.");
        RuleFor(request => request.Email)
            .Cascade(CascadeMode.Stop)
            .Must(email => email is null || email.Trim().Length <= OrganisationDetails.EmailMaxLength)
            .WithMessage($"'email' must be at most {OrganisationDetails.EmailMaxLength} characters.")
            .Must(email => email is null || IsEmail(email.Trim()))
            .WithMessage("'email' must be an email address.");
        RuleFor(request => request.Phone)
            .Cascade(CascadeMode.Stop)
            .Must(phone => phone is null || phone.Trim().Length <= OrganisationDetails.PhoneMaxLength)
            .WithMessage($"'phone' must be at most {OrganisationDetails.PhoneMaxLength} characters.")
            .Must(phone => phone is null || IsPhone(phone.Trim()))
            .WithMessage("'phone' must be a phone number: digits, with an optional leading '+', spaces, hyphens and brackets.");
    }

    /// <summary>An absolute http(s) URL with a host and no user information.</summary>
    private static bool IsWebAddress(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && uri.Host.Length > 0
        && uri.UserInfo.Length == 0;

    private static bool IsEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at == email.LastIndexOf('@') && at < email.Length - 1 && !email.Any(char.IsWhiteSpace);
    }

    private static bool IsPhone(string phone) =>
        phone.Count(char.IsAsciiDigit) >= 3
        && phone.Select((character, index) => char.IsAsciiDigit(character) || character is ' ' or '-' or '(' or ')' || (character == '+' && index == 0))
            .All(allowed => allowed);

    private static void Text(IRuleBuilderInitial<T, string?> rule, string field, int maxLength, bool required) => rule
        .Cascade(CascadeMode.Stop)
        .Must(text => !required || !string.IsNullOrWhiteSpace(text))
        .WithMessage($"'{field}' must not be empty.")
        .Must(text => text is null || text.Trim().Length <= maxLength)
        .WithMessage($"'{field}' must be at most {maxLength} characters.");
}

/// <summary>
/// The rules on organisation details that need more than the request's shape: a display name without a letter or a
/// digit has no name key (<c>ORGANISATION_NAME_INVALID</c>); a founded year after the tenant's year is refused on its
/// field (<c>VALIDATION_INVALID_REQUEST</c>); a primary venue must be a venue of the tenant that isn't closed or archived
/// (<c>ORGANISATION_PRIMARY_VENUE_UNAVAILABLE</c>), checked only when the request sets or changes it (S07 D4, D6).
/// </summary>
public sealed class OrganisationDetailsRules(RegistryDbContext context, TenantToday tenantToday)
{
    /// <summary>
    /// The details to save, trimmed, with <paramref name="defaultCountry"/> when none was sent; or the first refusal.
    /// <paramref name="currentPrimaryVenueId"/> is the organisation's venue before the change (null on a create): kept,
    /// it isn't checked again.
    /// </summary>
    public async Task<(OrganisationDetails? Details, FieldRefusal? Refusal)> CheckAsync(
        IOrganisationDetailsRequest request, string defaultCountry, Guid? currentPrimaryVenueId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Organisation.NameKeyOf(request.DisplayName).Length == 0)
        {
            return (null, new(RegistryErrors.OrganisationNameInvalid, "displayName"));
        }

        if (request.FoundedYear is { } year && year > (await tenantToday.GetAsync(cancellationToken)).Year)
        {
            return (null, new(CommonErrors.InvalidRequest, "foundedYear", "'foundedYear' can't be after this year (the tenant's date)."));
        }

        if (request.PrimaryVenueId is { } venueId && venueId != currentPrimaryVenueId)
        {
            // Untracked: the venue is another aggregate (S06 D11), and another tenant's is not found (RLS).
            var available = await context.Set<Venue>().AsNoTracking()
                .AnyAsync(v => v.Id == venueId && v.Status != VenueStatus.Closed && v.Status != VenueStatus.Archived, cancellationToken);
            if (!available)
            {
                return (null, new(RegistryErrors.OrganisationPrimaryVenueUnavailable, "primaryVenueId"));
            }
        }

        return (new OrganisationDetails(
            request.Type!.Value,
            request.DisplayName!.Trim(),
            Blank(request.LegalName),
            Blank(request.ShortName),
            request.Country ?? defaultCountry,
            Blank(request.City),
            request.FoundedYear,
            Blank(request.Website),
            Blank(request.Email),
            Blank(request.Phone),
            Blank(request.Description),
            request.PrimaryVenueId), null);
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
