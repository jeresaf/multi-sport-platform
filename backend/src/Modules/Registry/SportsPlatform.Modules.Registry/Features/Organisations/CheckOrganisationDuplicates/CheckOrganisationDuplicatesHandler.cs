using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Features.People;

namespace SportsPlatform.Modules.Registry.Features.Organisations.CheckOrganisationDuplicates;

/// <summary>
/// The live warning an organisation form shows before it saves (S07 D13): the same comparison the create runs, without
/// the lock and without saving. A display name without a letter or a digit is <c>ORGANISATION_NAME_INVALID</c>.
/// </summary>
public sealed class CheckOrganisationDuplicatesHandler(OrganisationDuplicateFinder finder)
{
    public async Task<(CheckOrganisationDuplicatesResponse? Answer, FieldRefusal? Refusal)> HandleAsync(
        CheckOrganisationDuplicatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var displayNameKey = Organisation.NameKeyOf(request.DisplayName);
        if (displayNameKey.Length == 0)
        {
            return (null, new(RegistryErrors.OrganisationNameInvalid, "displayName"));
        }

        var candidates = await finder.FindAsync(
            new OrganisationDuplicateInput(
                displayNameKey,
                Organisation.OptionalNameKeyOf(request.LegalName),
                Organisation.OptionalNameKeyOf(request.ShortName),
                request.Type!.Value,
                Organisation.CityKeyOf(request.City),
                OrganisationId: null),
            cancellationToken);
        return (new CheckOrganisationDuplicatesResponse([.. candidates.Select(OrganisationDuplicateCandidateResponse.From)]), null);
    }
}
