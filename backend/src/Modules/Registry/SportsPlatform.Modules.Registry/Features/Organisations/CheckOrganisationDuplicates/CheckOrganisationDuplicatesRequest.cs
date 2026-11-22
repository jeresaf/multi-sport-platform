using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.Organisations;

namespace SportsPlatform.Modules.Registry.Features.Organisations.CheckOrganisationDuplicates;

/// <summary>What the organisation duplicate warning compares (S07 D13): the names, type and city a save would send.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CheckOrganisationDuplicatesRequest(
    OrganisationType? Type, string? DisplayName, string? LegalName = null, string? ShortName = null, string? City = null);

/// <summary>The organisations that look like the one checked, strongest first (none is an empty list).</summary>
public sealed record CheckOrganisationDuplicatesResponse(IReadOnlyList<OrganisationDuplicateCandidateResponse> Candidates);

/// <summary>The check's shape: the type and the names and city as on an organisation.</summary>
public sealed class CheckOrganisationDuplicatesRequestValidator : AbstractValidator<CheckOrganisationDuplicatesRequest>
{
    public CheckOrganisationDuplicatesRequestValidator()
    {
        RuleFor(request => request.Type).NotNull().WithMessage("'type' is required.");
        Text(RuleFor(request => request.DisplayName), "displayName", OrganisationDetails.DisplayNameMaxLength, required: true);
        Text(RuleFor(request => request.LegalName), "legalName", OrganisationDetails.LegalNameMaxLength, required: false);
        Text(RuleFor(request => request.ShortName), "shortName", OrganisationDetails.ShortNameMaxLength, required: false);
        Text(RuleFor(request => request.City), "city", OrganisationDetails.CityMaxLength, required: false);
    }

    private static void Text(
        IRuleBuilderInitial<CheckOrganisationDuplicatesRequest, string?> rule, string field, int maxLength, bool required) => rule
        .Cascade(CascadeMode.Stop)
        .Must(text => !required || !string.IsNullOrWhiteSpace(text))
        .WithMessage($"'{field}' must not be empty.")
        .Must(text => text is null || text.Trim().Length <= maxLength)
        .WithMessage($"'{field}' must be at most {maxLength} characters.");
}
