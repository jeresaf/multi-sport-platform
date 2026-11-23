using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People;

namespace SportsPlatform.Modules.Registry.Features.Teams.CheckTeamDuplicates;

/// <summary>
/// The live warning the team form shows before it saves (S07 D13): the same comparison the create runs, without the
/// lock and without saving. A name without a letter or a digit is <c>TEAM_NAME_INVALID</c>. The variant isn't checked
/// against the enabled ones: an unknown one finds no team.
/// </summary>
public sealed class CheckTeamDuplicatesHandler(TeamDuplicateFinder finder)
{
    public async Task<(CheckTeamDuplicatesResponse? Answer, FieldRefusal? Refusal)> HandleAsync(
        CheckTeamDuplicatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nameKey = Team.NameKeyOf(request.Name);
        if (nameKey.Length == 0)
        {
            return (null, new(RegistryErrors.TeamNameInvalid, "name"));
        }

        var candidates = await finder.FindAsync(
            new TeamDuplicateInput(
                nameKey,
                Team.OptionalNameKeyOf(request.ShortName),
                request.Variant!.Trim(),
                new TeamCategory(request.GenderCategory!.Value, request.AgeGroup!.Value, request.Level ?? TeamLevel.First),
                request.OrganisationId,
                TeamId: null),
            cancellationToken);
        return (new CheckTeamDuplicatesResponse([.. candidates.Select(TeamDuplicateCandidateResponse.From)]), null);
    }
}
