using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
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
    string? DuplicateReason = null) : IVenueChangeRequest;

/// <summary>The request's shape: <see cref="VenueChangeValidator{T}"/>. Scoped: it reads the reference data.</summary>
public sealed class CreateVenueRequestValidator : AbstractValidator<CreateVenueRequest>
{
    public CreateVenueRequestValidator(IReferenceData reference) =>
        Include(new VenueChangeValidator<CreateVenueRequest>(reference));
}
