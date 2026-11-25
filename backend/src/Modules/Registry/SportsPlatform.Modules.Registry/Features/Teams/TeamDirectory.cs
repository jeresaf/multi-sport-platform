using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registry.Contracts.Teams;
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
/// <see cref="Team.NameOn"/>'s, so the teams are loaded with their aliases only when a date is asked for.
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
        IQueryable<Team> teams = context.Set<Team>().AsNoTracking();
        if (onDate is not null)
        {
            teams = teams.Include(t => t.Aliases);
        }

        var found = await teams.Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);
        return [.. found.Select(team => Summarise(team, onDate))];
    }

    private static TeamSummary Summarise(Team team, DateOnly? onDate)
    {
        var name = onDate is { } date ? team.NameOn(date) : new TeamNameOnDate(team.Name, team.ShortName, team.Code);
        return new(
            team.Id, name.Name, name.ShortName, name.Code, team.Variant, Map(team.GenderCategory), Map(team.AgeGroup),
            Map(team.Level), team.OrganisationId, Map(team.Status), team.Slug, team.LogoAssetId, team.BadgeAssetId);
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
