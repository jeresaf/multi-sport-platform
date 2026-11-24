using System.Globalization;
using System.Linq.Expressions;
using SportsPlatform.Modules.Registry.Domain.Teams;

namespace SportsPlatform.Modules.Registry.Features.Teams;

/// <summary>
/// The order of teams in the directory and the organisation read (S07 D19; owner, plan 2026-11-24): name (the sort key),
/// then category, then ID. The category ranks by age group in declared order (<c>SENIOR</c>, <c>U6</c> … <c>U23</c>,
/// <c>VETERANS</c>), then gender (<c>MEN</c>, <c>WOMEN</c>, <c>MIXED</c>), then level (<c>FIRST</c>, <c>RESERVE</c>,
/// <c>DEVELOPMENT</c>, <c>ACADEMY</c>). The columns hold the codes as text, whose order isn't this one (<c>U10</c>
/// before <c>U6</c>), so the rank is computed: age group × 100 + gender × 10 + level, by the enums' positions.
/// </summary>
public static class TeamOrder
{
    private const int _ageGroupWeight = 100;
    private const int _genderWeight = 10;

    /// <summary>The category rank of a team, as SQL (a <c>CASE</c> per part).</summary>
    public static Expression<Func<Team, int>> CategoryRank { get; } = BuildCategoryRank();

    /// <summary>The teams with their category rank, so a query can sort and page by it.</summary>
    public static IQueryable<RankedTeam> Ranked(IQueryable<Team> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        return teams.Select(_ranked);
    }

    /// <summary>The teams in this order.</summary>
    public static IOrderedQueryable<RankedTeam> Ordered(IQueryable<RankedTeam> teams) =>
        teams.OrderBy(t => t.Team.SortKey).ThenBy(t => t.CategoryRank).ThenBy(t => t.Team.Id);

    public static int CategoryRankOf(TeamCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return (Position(category.AgeGroup) * _ageGroupWeight) + (Position(category.Gender) * _genderWeight) + Position(category.Level);
    }

    /// <summary>Whether <paramref name="rank"/> is the rank of some category (a cursor's rank must be).</summary>
    public static bool IsCategoryRank(int rank) =>
        rank >= 0
        && rank / _ageGroupWeight < Enum.GetValues<AgeGroup>().Length
        && rank % _ageGroupWeight / _genderWeight < Enum.GetValues<GenderCategory>().Length
        && rank % _genderWeight < Enum.GetValues<TeamLevel>().Length;

    // t => new RankedTeam { Team = t, CategoryRank = <CategoryRank's body> }
    private static readonly Expression<Func<Team, RankedTeam>> _ranked = Expression.Lambda<Func<Team, RankedTeam>>(
        Expression.MemberInit(
            Expression.New(typeof(RankedTeam)),
            Expression.Bind(typeof(RankedTeam).GetProperty(nameof(RankedTeam.Team))!, CategoryRank.Parameters[0]),
            Expression.Bind(typeof(RankedTeam).GetProperty(nameof(RankedTeam.CategoryRank))!, CategoryRank.Body)),
        CategoryRank.Parameters[0]);

    private static Expression<Func<Team, int>> BuildCategoryRank()
    {
        // The parts must fit their weights, or two categories would share a rank.
        if (Enum.GetValues<GenderCategory>().Length > _ageGroupWeight / _genderWeight
            || Enum.GetValues<TeamLevel>().Length > _genderWeight)
        {
            throw new InvalidOperationException("A category part outgrew its weight in the team order.");
        }

        var team = Expression.Parameter(typeof(Team), "t");
        var rank = Expression.Add(
            Expression.Add(
                Expression.Multiply(PositionOf<AgeGroup>(Expression.Property(team, nameof(Team.AgeGroup))), Expression.Constant(_ageGroupWeight)),
                Expression.Multiply(PositionOf<GenderCategory>(Expression.Property(team, nameof(Team.GenderCategory))), Expression.Constant(_genderWeight))),
            PositionOf<TeamLevel>(Expression.Property(team, nameof(Team.Level))));
        return Expression.Lambda<Func<Team, int>>(rank, team);
    }

    // value == first ? 0 : value == second ? 1 : … : last position. Compared as the compiler writes an enum comparison
    // (both sides converted to int), which EF translates through the column's converter.
    private static Expression PositionOf<TEnum>(Expression value)
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        Expression position = Expression.Constant(values.Length - 1);
        for (var index = values.Length - 2; index >= 0; index--)
        {
            var test = Expression.Equal(
                Expression.Convert(value, typeof(int)),
                Expression.Convert(Expression.Constant(values[index]), typeof(int)));
            position = Expression.Condition(test, Expression.Constant(index), position);
        }

        return position;
    }

    private static int Position<TEnum>(TEnum value)
        where TEnum : struct, Enum => Array.IndexOf(Enum.GetValues<TEnum>(), value);
}

/// <summary>A team with its category rank (<see cref="TeamOrder"/>), inside a query.</summary>
public sealed class RankedTeam
{
    public Team Team { get; init; } = null!;

    public int CategoryRank { get; init; }
}

/// <summary>
/// The opaque cursor of the teams directory: the sort key, category rank (<see cref="TeamOrder"/>) and ID of a page's
/// last team, base64url-encoded.
/// </summary>
public sealed record TeamDirectoryCursor(string SortKey, int CategoryRank, Guid Id)
{
    public string Encode() => CursorText.Encode(
        SortKey, CategoryRank.ToString(CultureInfo.InvariantCulture), Id.ToString("N", CultureInfo.InvariantCulture));

    /// <summary>Decodes a cursor this directory gave, encoded exactly as <see cref="Encode"/> does.</summary>
    public static bool TryDecode(string? text, out TeamDirectoryCursor cursor)
    {
        cursor = new TeamDirectoryCursor(string.Empty, 0, Guid.Empty);
        if (!CursorText.TryDecode(text, TeamDetails.NameMaxLength, 3, out var parts)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rank)
            || !TeamOrder.IsCategoryRank(rank)
            || !Guid.TryParseExact(parts[2], "N", out var id))
        {
            return false;
        }

        var candidate = new TeamDirectoryCursor(parts[0], rank, id);
        if (candidate.Encode() != text)
        {
            return false;
        }

        cursor = candidate;
        return true;
    }
}
