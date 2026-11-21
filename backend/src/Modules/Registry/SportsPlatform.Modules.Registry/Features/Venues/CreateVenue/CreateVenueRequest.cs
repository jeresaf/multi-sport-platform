using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues.CreateVenue;

/// <summary>
/// A new venue (S07 D3): its fields, an optional slug (proposed from the name when none is sent, D9), and, after the
/// duplicate warning, <c>duplicatesReviewed</c> with an optional reason (D13).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateVenueRequest(
    string? Name,
    VenueType? Type,
    string? City,
    string? TimeZone,
    string? ShortName = null,
    string? AddressLine = null,
    string? Region = null,
    string? Country = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? Capacity = null,
    IReadOnlyList<VenueSurface>? Surfaces = null,
    IReadOnlyList<string?>? SupportedVariants = null,
    string? Slug = null,
    bool DuplicatesReviewed = false,
    string? DuplicateReason = null) : IVenueDetailsRequest
{
    public const int DuplicateReasonMaxLength = 500;
}

/// <summary>
/// The request's shape: <see cref="VenueDetailsValidator{T}"/>, a well-formed slug that isn't a UUID, and a reason that
/// isn't blank and comes only with <c>duplicatesReviewed</c>. Scoped: it reads the reference data.
/// </summary>
public sealed class CreateVenueRequestValidator : AbstractValidator<CreateVenueRequest>
{
    public CreateVenueRequestValidator(IReferenceData reference)
    {
        Include(new VenueDetailsValidator<CreateVenueRequest>(reference));
        RuleFor(request => request.Slug)
            .Must(slug => slug is null || Slugs.IsValid(slug))
            .WithMessage(
                $"'slug' must be {Slugs.MinLength}–{Slugs.MaxLength} lower-case letters and digits in words joined by single hyphens, and not a UUID.");
        RuleFor(request => request.DuplicateReason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'duplicateReason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= CreateVenueRequest.DuplicateReasonMaxLength)
            .WithMessage($"'duplicateReason' must be at most {CreateVenueRequest.DuplicateReasonMaxLength} characters.");
        RuleFor(request => request.DuplicateReason)
            .Null().When(request => !request.DuplicatesReviewed)
            .WithMessage("'duplicateReason' comes only with 'duplicatesReviewed'.");
    }
}
