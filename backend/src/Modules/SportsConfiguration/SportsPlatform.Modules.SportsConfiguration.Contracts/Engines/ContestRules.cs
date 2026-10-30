namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// The effective rules a contest plays by, flattened to dotted paths as the definition's limits and the templates'
/// editable parameters name them (<c>substitutions.max</c>, <c>contest.period_seconds.FIRST_HALF</c>;
/// docs/domain/rulesets.md). Values are whole numbers, flags, text or lists of text.
/// </summary>
public sealed class ContestRules
{
    private readonly Dictionary<string, object> _values;

    public ContestRules(IEnumerable<KeyValuePair<string, object>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (path, value) in values)
        {
            _values.Add(path, value switch
            {
                long or bool or string => value,
                int number => (long)number,
                IEnumerable<string> list => (IReadOnlyList<string>)[.. list],
                _ => throw new ArgumentException($"Rule {path} has a {value?.GetType().Name ?? "null"} value; rules are numbers, flags, text or lists of text.", nameof(values)),
            });
        }
    }

    public static ContestRules None { get; } = new([]);

    public IReadOnlyCollection<string> Paths => _values.Keys;

    public long? Number(string path) => Get<long>(path) is { } found ? found.Value : null;

    public bool? Flag(string path) => Get<bool>(path) is { } found ? found.Value : null;

    public string? Text(string path) => Get<string>(path)?.Value;

    public IReadOnlyList<string>? List(string path) => Get<IReadOnlyList<string>>(path)?.Value;

    // A rule of another type than the caller expects is a bug in the template or the engine, not a missing rule.
    private Found<T>? Get<T>(string path) => _values.TryGetValue(path, out var value)
        ? value is T typed ? new Found<T>(typed) : throw new InvalidOperationException($"Rule {path} is not a {typeof(T).Name}.")
        : null;

    private sealed record Found<T>(T Value);
}
