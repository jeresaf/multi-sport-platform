using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Rulesets;

/// <summary>
/// The resolver's answer: a <see cref="Snapshot"/>, or the <see cref="Problems"/> that stop one, one per field and
/// ordered by field. Exactly one of the two is present.
/// </summary>
public sealed record RulesetResolution
{
    private RulesetResolution(RulesSnapshot? snapshot, IReadOnlyList<RulesetProblem> problems)
    {
        Snapshot = snapshot;
        Problems = problems;
    }

    public RulesSnapshot? Snapshot { get; }

    public IReadOnlyList<RulesetProblem> Problems { get; }

    public bool IsSuccess => Snapshot is not null;

    public static RulesetResolution Success(RulesSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(snapshot, []);
    }

    public static RulesetResolution Failure(IEnumerable<RulesetProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        // One problem per field (a form marks each field once): the first one's error, every distinct message.
        RulesetProblem[] list = [.. problems
            .GroupBy(problem => problem.Field, StringComparer.Ordinal)
            .Select(field => field.First() with { Message = string.Join("; ", field.Select(problem => problem.Message).Distinct(StringComparer.Ordinal)) })
            .OrderBy(problem => problem.Field, StringComparer.Ordinal)];
        if (list.Length == 0)
        {
            throw new ArgumentException("A failed resolution has at least one problem.", nameof(problems));
        }

        return new(null, list);
    }
}

/// <summary>
/// A problem with one field of the request: <see cref="Field"/> follows the request's shape (<c>overrides.roster.min</c>,
/// <c>format.code</c>, <c>format.version</c>, <c>format.parameters.cycles</c>, <c>template</c>,
/// <c>template.version</c>); <see cref="Error"/> is its stable code; <see cref="Message"/> says what is wrong in words a
/// competition administrator follows.
/// </summary>
public sealed record RulesetProblem(string Field, Error Error, string Message);
