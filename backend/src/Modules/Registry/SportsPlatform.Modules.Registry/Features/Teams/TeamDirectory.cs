using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Contracts.Teams;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Infrastructure;
using AgeGroup = SportsPlatform.Modules.Registry.Contracts.Teams.AgeGroup;
using DomainAgeGroup = SportsPlatform.Modules.Registry.Domain.Teams.AgeGroup;
using DomainGender = SportsPlatform.Modules.Registry.Domain.Teams.GenderCategory;
using DomainLevel = SportsPlatform.Modules.Registry.Domain.Teams.TeamLevel;
using DomainStatus = SportsPlatform.Modules.Registry.Domain.Teams.TeamStatus;
using GenderCategory = SportsPlatform.Modules.Registry.Contracts.Teams.GenderCategory;
using TeamLevel = SportsPlatform.Modules.Registry.Contracts.Teams.TeamLevel;
using TeamStatus = SportsPlatform.Modules.Registry.Contracts.Teams.TeamStatus;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary>
/// <see cref="ITeamDirectory"/> over the scope's tenant (query filter and RLS). The name on a date is
/// <see cref="Team.NameOn"/>'s, so the teams are loaded with their former names (not their nicknames or spellings) only
/// when a date is asked for; without one,
/// only the summary's columns are read (as <c>OrganisationDirectory</c>).
/// </summary>
public sealed class TeamDirectory(RegistryDbContext context) : ITeamDirectory
{
    public async Task<IReadOnlyList<TeamSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> teamIds, DateOnly? onDate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        if (teamIds.Count == 0)
        {
            return [];
        }

        var ids = teamIds.Distinct().ToArray();
        if (onDate is not { } date)
        {
            var rows = await context.Set<Team>().AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.ShortName,
                    t.Code,
                    t.Variant,
                    t.GenderCategory,
                    t.AgeGroup,
                    t.Level,
                    t.OrganisationId,
                    t.Status,
                    t.Slug,
                    t.LogoAssetId,
                    t.BadgeAssetId,
                })
                .ToListAsync(cancellationToken);
            return [.. rows.Select(t => new TeamSummary(
                t.Id, t.Name, t.ShortName, t.Code, t.Variant, Map(t.GenderCategory), Map(t.AgeGroup), Map(t.Level),
                t.OrganisationId, Map(t.Status), t.Slug, t.LogoAssetId, t.BadgeAssetId))];
        }

        var teams = await context.Set<Team>().AsNoTracking()
            .Include(t => t.Aliases.Where(a => a.Kind == AliasKind.FormerName))
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(cancellationToken);
        return [.. teams.Select(team =>
        {
            var name = team.NameOn(date);
            return new TeamSummary(
                team.Id, name.Name, name.ShortName, name.Code, team.Variant, Map(team.GenderCategory), Map(team.AgeGroup),
                Map(team.Level), team.OrganisationId, Map(team.Status), team.Slug, team.LogoAssetId, team.BadgeAssetId);
        })];
    }

    public static TeamStatus Map(DomainStatus status) => status switch
    {
        DomainStatus.Active => TeamStatus.Active,
        DomainStatus.Inactive => TeamStatus.Inactive,
        DomainStatus.Suspended => TeamStatus.Suspended,
        DomainStatus.Disbanded => TeamStatus.Disbanded,
        DomainStatus.Archived => TeamStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static GenderCategory Map(DomainGender gender) => gender switch
    {
        DomainGender.Men => GenderCategory.Men,
        DomainGender.Women => GenderCategory.Women,
        DomainGender.Mixed => GenderCategory.Mixed,
        _ => throw new ArgumentOutOfRangeException(nameof(gender)),
    };

    public static TeamLevel Map(DomainLevel level) => level switch
    {
        DomainLevel.First => TeamLevel.First,
        DomainLevel.Reserve => TeamLevel.Reserve,
        DomainLevel.Development => TeamLevel.Development,
        DomainLevel.Academy => TeamLevel.Academy,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    public static AgeGroup Map(DomainAgeGroup ageGroup) => ageGroup switch
    {
        DomainAgeGroup.Senior => AgeGroup.Senior,
        DomainAgeGroup.U6 => AgeGroup.U6,
        DomainAgeGroup.U7 => AgeGroup.U7,
        DomainAgeGroup.U8 => AgeGroup.U8,
        DomainAgeGroup.U9 => AgeGroup.U9,
        DomainAgeGroup.U10 => AgeGroup.U10,
        DomainAgeGroup.U11 => AgeGroup.U11,
        DomainAgeGroup.U12 => AgeGroup.U12,
        DomainAgeGroup.U13 => AgeGroup.U13,
        DomainAgeGroup.U14 => AgeGroup.U14,
        DomainAgeGroup.U15 => AgeGroup.U15,
        DomainAgeGroup.U16 => AgeGroup.U16,
        DomainAgeGroup.U17 => AgeGroup.U17,
        DomainAgeGroup.U18 => AgeGroup.U18,
        DomainAgeGroup.U19 => AgeGroup.U19,
        DomainAgeGroup.U20 => AgeGroup.U20,
        DomainAgeGroup.U21 => AgeGroup.U21,
        DomainAgeGroup.U22 => AgeGroup.U22,
        DomainAgeGroup.U23 => AgeGroup.U23,
        DomainAgeGroup.Veterans => AgeGroup.Veterans,
        _ => throw new ArgumentOutOfRangeException(nameof(ageGroup)),
    };
}
