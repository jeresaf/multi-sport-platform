using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.Organisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.UpdateOrganisation;

/// <summary>
/// An organisation's editable fields, replaced whole (S07 D11): a field left out is cleared; <c>country</c> left out is
/// the tenant's, as on create; <c>slug</c> left out keeps the organisation's (D9; owner, T2b plan, as venues'). After the
/// duplicate warning (a change to a name, the type or the city, D13), <c>duplicatesReviewed</c> with an optional reason.
/// <c>version</c>, when sent, must be the organisation's current one (or <c>If-Match</c>).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateOrganisationRequest(
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
    string? DuplicateReason = null,
    uint? Version = null) : IOrganisationChangeRequest;

/// <summary>The request's shape: <see cref="OrganisationChangeValidator{T}"/>. Scoped: it reads the reference data.</summary>
public sealed class UpdateOrganisationRequestValidator : AbstractValidator<UpdateOrganisationRequest>
{
    public UpdateOrganisationRequestValidator(IReferenceData reference) =>
        Include(new OrganisationChangeValidator<UpdateOrganisationRequest>(reference));
}
