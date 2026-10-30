namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Event types every definition has without listing them (docs/domain/sports/README.md, "Generic event types").
/// </summary>
public static class GenericEventTypes
{
    public static IReadOnlySet<string> Codes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "PERIOD_START", "PERIOD_END", "CLOCK_STOP", "CLOCK_START", "CLOCK_SET", "STATUS_CHANGED", "EVENT_REVERSED", "NOTE",
    };
}
