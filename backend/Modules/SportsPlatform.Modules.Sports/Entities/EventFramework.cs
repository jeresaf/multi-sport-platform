using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Sports.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventTimingRequirement
{
    None,
    Timestamp,
    Clock,
    TimestampOrClock
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventCorrectionPolicy
{
    Prohibited,
    ApprovalRequired
}

public sealed record EventValidationRules(
    decimal? MinimumValue,
    decimal? MaximumValue,
    decimal? MinimumClockSeconds,
    decimal? MaximumClockSeconds,
    string RulesJson);

public sealed record EventDefinition(
    string Code,
    string Name,
    IReadOnlyList<string> SegmentApplicability,
    bool RequiresParticipant,
    bool RequiresSecondaryParticipant,
    string? RequiredMetric,
    EventTimingRequirement TimingRequirement,
    EventCorrectionPolicy CorrectionPolicy,
    EventValidationRules Validation);

public sealed record EventPayload(
    Guid? ParticipantId,
    Guid? SecondaryParticipantId,
    string MetricName,
    decimal Value,
    string? SegmentId,
    string? SegmentName,
    string? SegmentType,
    decimal? ClockSeconds,
    DateTime? Timestamp);

public static class EventFramework
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IReadOnlyList<EventDefinition> ParseDefinitions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Event definitions must be a JSON array.", nameof(json));
        var definitions = document.RootElement.EnumerateArray().Select(ParseDefinition).ToArray();
        var duplicate = definitions.GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Event code '{duplicate.Key}' is duplicated.", nameof(json));
        return definitions;
    }

    public static string NormalizeDefinitions(string json)
    {
        var root = JsonNode.Parse(json) as JsonArray
            ?? throw new ArgumentException("Event definitions must be a JSON array.", nameof(json));
        ParseDefinitions(json);
        foreach (var node in root.OfType<JsonObject>())
        {
            var definition = ParseDefinition(JsonSerializer.SerializeToElement(node));
            node["segmentApplicability"] ??= new JsonArray(
                definition.SegmentApplicability
                    .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            node["requiresParticipant"] = definition.RequiresParticipant;
            node["requiresSecondaryParticipant"] ??= definition.RequiresSecondaryParticipant;
            if (!node.ContainsKey("requiredMetric")) node["requiredMetric"] = definition.RequiredMetric;
            node["timingRequirement"] ??= definition.TimingRequirement.ToString();
            node["correctionPolicy"] ??= definition.CorrectionPolicy.ToString();
            node["validation"] ??= new JsonObject();
        }
        return root.ToJsonString(JsonOptions);
    }

    public static EventDefinition ParseDefinition(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Every event definition must be an object.");
        var code = RequiredString(element, "id", "code");
        var name = RequiredString(element, "name");
        var segments = Strings(element, "segmentApplicability", "segments", "segmentTypes");
        if (segments.Count == 0) segments = new[] { "*" };
        if (segments.Any(string.IsNullOrWhiteSpace)
            || segments.Distinct(StringComparer.OrdinalIgnoreCase).Count() != segments.Count)
            throw new ArgumentException($"Event '{code}' contains invalid or duplicate segment applicability.");

        var requiresParticipant = Boolean(element, "requiresParticipant") ?? false;
        var requiresSecondary = Boolean(element, "requiresSecondaryParticipant") ?? false;
        var requiredMetric = String(element, "requiredMetric");
        var timingText = String(element, "timingRequirement")
            ?? (Boolean(element, "requiresMinute") == true ? nameof(EventTimingRequirement.TimestampOrClock) : nameof(EventTimingRequirement.None));
        if (!Enum.TryParse<EventTimingRequirement>(timingText?.Replace(" ", string.Empty), true, out var timing))
            throw new ArgumentException($"Event '{code}' timing requirement '{timingText}' is not supported.");
        var correctionText = String(element, "correctionPolicy")
            ?? nameof(EventCorrectionPolicy.ApprovalRequired);
        if (!Enum.TryParse<EventCorrectionPolicy>(correctionText?.Replace(" ", string.Empty), true, out var correction))
            throw new ArgumentException($"Event '{code}' correction policy '{correctionText}' is not supported.");

        var validation = Property(element, "validation");
        if (validation is { ValueKind: not JsonValueKind.Object })
            throw new ArgumentException($"Event '{code}' validation must be an object.");
        var minimumValue = Decimal(validation, "minimumValue", "minValue");
        var maximumValue = Decimal(validation, "maximumValue", "maxValue");
        var minimumClock = Decimal(validation, "minimumClockSeconds", "minClockSeconds");
        var maximumClock = Decimal(validation, "maximumClockSeconds", "maxClockSeconds");
        ValidateRange(code, "value", minimumValue, maximumValue);
        ValidateRange(code, "clock", minimumClock, maximumClock);
        if (minimumClock is < 0)
            throw new ArgumentException($"Event '{code}' minimum clock seconds cannot be negative.");
        var rulesJson = validation?.GetRawText() ?? "{}";

        return new EventDefinition(
            code, name, segments, requiresParticipant, requiresSecondary,
            requiredMetric, timing, correction,
            new EventValidationRules(minimumValue, maximumValue, minimumClock, maximumClock, rulesJson));
    }

    public static void ValidatePayload(EventDefinition definition, EventPayload payload)
    {
        if (definition.RequiresParticipant && (!payload.ParticipantId.HasValue || payload.ParticipantId == Guid.Empty))
            throw new ArgumentException($"Event '{definition.Code}' requires a participant.");
        if (definition.RequiresSecondaryParticipant
            && (!payload.SecondaryParticipantId.HasValue || payload.SecondaryParticipantId == Guid.Empty))
            throw new ArgumentException($"Event '{definition.Code}' requires a secondary participant.");
        if (payload.ParticipantId.HasValue && payload.ParticipantId == payload.SecondaryParticipantId)
            throw new ArgumentException("The primary and secondary participants must be different.");
        if (definition.RequiredMetric is not null
            && !definition.RequiredMetric.Equals(payload.MetricName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Event '{definition.Code}' requires metric '{definition.RequiredMetric}'.");

        if (!definition.SegmentApplicability.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            if (payload.SegmentId is null)
                throw new ArgumentException($"Event '{definition.Code}' requires an applicable segment.");
            var matches = definition.SegmentApplicability.Any(item =>
                item.Equals(payload.SegmentId, StringComparison.OrdinalIgnoreCase)
                || item.Equals(payload.SegmentName, StringComparison.OrdinalIgnoreCase)
                || item.Equals(payload.SegmentType, StringComparison.OrdinalIgnoreCase));
            if (!matches)
                throw new ArgumentException($"Event '{definition.Code}' is not applicable to the selected segment.");
        }

        var hasTimestamp = payload.Timestamp.HasValue;
        var hasClock = payload.ClockSeconds.HasValue;
        if (definition.TimingRequirement == EventTimingRequirement.Timestamp && !hasTimestamp)
            throw new ArgumentException($"Event '{definition.Code}' requires a timestamp.");
        if (definition.TimingRequirement == EventTimingRequirement.Clock && !hasClock)
            throw new ArgumentException($"Event '{definition.Code}' requires a contest clock.");
        if (definition.TimingRequirement == EventTimingRequirement.TimestampOrClock && !hasTimestamp && !hasClock)
            throw new ArgumentException($"Event '{definition.Code}' requires a timestamp or contest clock.");

        if (definition.Validation.MinimumValue is not null && payload.Value < definition.Validation.MinimumValue)
            throw new ArgumentException($"Event '{definition.Code}' value must be at least {definition.Validation.MinimumValue}.");
        if (definition.Validation.MaximumValue is not null && payload.Value > definition.Validation.MaximumValue)
            throw new ArgumentException($"Event '{definition.Code}' value must not exceed {definition.Validation.MaximumValue}.");
        if (payload.ClockSeconds is not null
            && definition.Validation.MinimumClockSeconds is not null
            && payload.ClockSeconds < definition.Validation.MinimumClockSeconds)
            throw new ArgumentException($"Event '{definition.Code}' clock must be at least {definition.Validation.MinimumClockSeconds} seconds.");
        if (payload.ClockSeconds is not null
            && definition.Validation.MaximumClockSeconds is not null
            && payload.ClockSeconds > definition.Validation.MaximumClockSeconds)
            throw new ArgumentException($"Event '{definition.Code}' clock must not exceed {definition.Validation.MaximumClockSeconds} seconds.");
    }

    private static void ValidateRange(
        string code, string label, decimal? minimum, decimal? maximum)
    {
        if (minimum is not null && maximum is not null && minimum > maximum)
            throw new ArgumentException($"Event '{code}' {label} minimum cannot exceed its maximum.");
    }

    private static string RequiredString(JsonElement element, params string[] names) =>
        String(element, names) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Event definition requires '{names[0]}'.");

    private static string? String(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (TryProperty(element.Value, name, out var property)
                && property.ValueKind == JsonValueKind.String)
                return property.GetString()?.Trim();
        return null;
    }

    private static bool? Boolean(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (TryProperty(element.Value, name, out var property)
                && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return property.GetBoolean();
        return null;
    }

    private static decimal? Decimal(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (TryProperty(element.Value, name, out var property)
                && property.TryGetDecimal(out var value)) return value;
        return null;
    }

    private static IReadOnlyList<string> Strings(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryProperty(element, name, out var property)
                || property.ValueKind != JsonValueKind.Array) continue;
            return property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim() ?? string.Empty)
                .ToArray();
        }
        return Array.Empty<string>();
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        TryProperty(element, name, out var property) ? property : null;

    private static bool TryProperty(JsonElement element, string name, out JsonElement property)
    {
        foreach (var item in element.EnumerateObject())
            if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        property = default;
        return false;
    }
}
