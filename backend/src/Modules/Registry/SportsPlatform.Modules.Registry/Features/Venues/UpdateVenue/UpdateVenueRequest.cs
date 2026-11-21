using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.Venues;

namespace SportsPlatform.Modules.Registry.Features.Venues.UpdateVenue;

/// <summary>
/// A venue's editable fields, replaced whole (S07 D11): a field left out is cleared; <c>country</c> left out is the
/// tenant's, as on create; <c>slug</c> left out keeps the venue's (D9; owner, T1b plan). After the duplicate warning
/// (a change to the name, city or coordinates, D13), <c>duplicatesReviewed</c> with an optional reason. <c>version</c>,
/// when sent, must be the venue's current one (or <c>If-Match</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateVenueRequest(
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
    string? DuplicateReason = null,
    uint? Version = null) : IVenueChangeRequest;

/// <summary>The request's shape: <see cref="VenueChangeValidator{T}"/>. Scoped: it reads the reference data.</summary>
public sealed class UpdateVenueRequestValidator : AbstractValidator<UpdateVenueRequest>
{
    public UpdateVenueRequestValidator(IReferenceData reference) =>
        Include(new VenueChangeValidator<UpdateVenueRequest>(reference));
}
