using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Features.People;
using SportsPlatform.Modules.Registry.Features.Teams.CreateTeam;
using SportsPlatform.Modules.Registry.Infrastructure;

namespace SportsPlatform.Modules.Registry.Features.Teams.RenameTeam;

/// <summary>
/// Renames a team from a date (S07 D8): the old name, with its short name and code, becomes a former name from the old
/// name's start to the day before <c>effectiveOn</c>, and the slug stays (D9). A name without a letter or digit is
/// <c>TEAM_NAME_INVALID</c>; then the version; then the team's rules (<see cref="Team.Rename"/>: an archived or disbanded
/// team, the date, the same name). When the name's or short name's key changes, the rename holds
/// <c>registry.teams:&lt;tenantId&gt;</c> while the duplicate warning runs again for the new names, the team itself left
/// out (D13, D21): teams that look like the renamed one refuse it with <c>TEAM_DUPLICATE_REVIEW_REQUIRED</c> and the
/// candidates until the request says <c>duplicatesReviewed</c>; the audit entry names them and the reason. The new
/// former-name row moves the team's version (S06 D11).
/// </summary>
public sealed class RenameTeamHandler(
    RegistryDbContext context,
    TeamDuplicateFinder finder,
    TenantToday tenantToday,
    ITenantContext tenantContext,
    AuditScope audit)
{
    /// <summary>Null when saved and committed; otherwise the refusal.</summary>
    public async Task<ProblemHttpResult?> HandleAsync(
        Guid teamId, RenameTeamRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ifMatch);
        var tenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException("A team is renamed under the request's tenant.");

        // Described before anything can refuse, so a refused rename is audited with its reason.
        var reason = string.IsNullOrWhiteSpace(request.DuplicateReason) ? null : request.DuplicateReason.Trim();
        audit.Describe(TeamsEndpoints.EntityType, teamId, reason);

        var name = request.Name!.Trim();
        if (Team.NameKeyOf(name).Length == 0)
        {
            return new FieldRefusal(RegistryErrors.TeamNameInvalid, "name").ToProblem();
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var (team, refusal) = await TeamChanges.LoadAsync(context, teamId, request.Version, ifMatch, cancellationToken);
        if (team is null)
        {
            return refusal!.ToProblem();
        }

        var shortName = request.ShortNameSent ? Blank(request.ShortName) : team.ShortName;
        var code = request.CodeSent ? request.Code : team.Code;
        var keysChange = Team.NameKeyOf(name) != team.NameKey || Team.OptionalNameKeyOf(shortName) != team.ShortNameKey;
        var today = await tenantToday.GetAsync(cancellationToken);
        var renamed = team.Rename(name, shortName, code, request.EffectiveOn!.Value, today);
        if (renamed.IsFailure)
        {
            return new FieldRefusal(renamed.Error, FieldOf(renamed.Error)).ToProblem();
        }

        if (keysChange)
        {
            await TeamChanges.LockTeamsAsync(context, tenantId, cancellationToken);
            if (await ReviewDuplicatesAsync(team, request.DuplicatesReviewed, cancellationToken) is { } stop)
            {
                return stop;
            }
        }

        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error.ToProblem();
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }

    private static string? FieldOf(Error error) =>
        error.Code == RegistryErrors.TeamRenameDateInvalid.Code ? "effectiveOn"
        : error.Code == RegistryErrors.TeamNameUnchanged.Code ? "name"
        : null;

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // D13's warning for the renamed team (its new names, its variant, category and organisation), the team itself left
    // out; null to go on. Read after the lock: a new statement, so in READ COMMITTED it sees every team committed before it.
    private async Task<ProblemHttpResult?> ReviewDuplicatesAsync(Team team, bool reviewed, CancellationToken cancellationToken)
    {
        var input = new TeamDuplicateInput(team.NameKey, team.ShortNameKey, team.Variant, team.Category, team.OrganisationId, team.Id);
        var candidates = await finder.FindAsync(input, cancellationToken);
        audit.Describe(metadata: new TeamDuplicateReviewAuditMetadata(
            [.. candidates.Select(candidate => new TeamDuplicateCandidateAudit(candidate.TeamId, candidate.Confidence))]));
        if (candidates.Count == 0 || reviewed)
        {
            return null;
        }

        var problem = new FieldRefusal(RegistryErrors.TeamDuplicateReviewRequired, "duplicatesReviewed").ToProblem();
        problem.ProblemDetails.Extensions[CreateTeamHandler.CandidatesExtension] =
            candidates.Select(TeamDuplicateCandidateResponse.From).ToList();
        return problem;
    }
}
