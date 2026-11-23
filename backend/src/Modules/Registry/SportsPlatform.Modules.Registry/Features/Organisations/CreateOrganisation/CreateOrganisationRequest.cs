using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.Organisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.CreateOrganisation;

/// <summary>
/// A new organisation (S07 D4): its fields, an optional slug (proposed from the display name when none is sent, D9),
/// and, after the duplicate warning, <c>duplicatesReviewed</c> with an optional reason (D13).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrganisationRequest(
    OrganisationType? Type,
    string? DisplayName,
    string? LegalName = null,
    string? ShortName = null,
    string? Country = null,
    string? City = null,
    int? FoundedYear = null,
    string? Website = null,
    string? Email = null,
    string? Phone = null,
    string? Description = null,
    Guid? PrimaryVenueId = null,
    string? Slug = null,
    bool DuplicatesReviewed = false,
    string? DuplicateReason = null) : IOrganisationChangeRequest;

/// <summary>The request's shape: <see cref="OrganisationChangeValidator{T}"/>. Scoped: it reads the reference data.</summary>
public sealed class CreateOrganisationRequestValidator : AbstractValidator<CreateOrganisationRequest>
{
    public CreateOrganisationRequestValidator(IReferenceData reference) =>
        Include(new OrganisationChangeValidator<CreateOrganisationRequest>(reference));
}
