using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;

namespace SportsPlatform.Modules.SportsConfiguration.Definitions;

/// <summary>
/// Event types every definition has without listing them (docs/domain/sports/README.md, "Generic event types"), with
/// the table's categories. README.md gives no labels: these are written from the codes.
/// </summary>
public static class GenericEventTypes
{
    public static IReadOnlyList<GenericEventTypeDefinition> All { get; } =
    [
        new("PERIOD_START", EventCategory.Period, "Period start"),
        new("PERIOD_END", EventCategory.Period, "Period end"),
        new("CLOCK_STOP", EventCategory.System, "Clock stopped"),
        new("CLOCK_START", EventCategory.System, "Clock started"),
        new("CLOCK_SET", EventCategory.System, "Clock set"),
        new("STATUS_CHANGED", EventCategory.System, "Status changed"),
        new("EVENT_REVERSED", EventCategory.Correction, "Event reversed"),
        new("NOTE", EventCategory.Commentary, "Note"),
    ];

    public static IReadOnlySet<string> Codes { get; } = All.Select(type => type.Code).ToHashSet(StringComparer.Ordinal);
}
