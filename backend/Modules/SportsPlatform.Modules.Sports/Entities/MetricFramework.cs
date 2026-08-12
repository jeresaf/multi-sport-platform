using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SportsPlatform.Modules.Sports.Entities;

public enum MetricDataType
{
    Integer,
    Decimal,
    Duration,
    ClockTime,
    Distance,
    Weight,
    Boolean,
    Text,
    Enumeration,
    CompositeScore
}

public enum MetricSortDirection
{
    None,
    Ascending,
    Descending
}

public sealed record MetricValidation(
    decimal? Minimum,
    decimal? Maximum,
    int Precision,
    int? MaximumLength,
    string? Pattern,
    IReadOnlyList<string> AllowedValues,
    IReadOnlyList<string> Components);

public sealed record MetricDefinition(
    string Code,
    string Name,
    MetricDataType DataType,
    string? Unit,
    MetricSortDirection SortDirection,
    MetricValidation Validation);

public sealed record MetricValue(
    MetricDataType DataType,
    string SerializedValue,
    string? Unit,
    int Precision,
    decimal? NumericValue,
    string? TextValue,
    IReadOnlyList<decimal> CompositeParts);

public sealed record MetricRankCandidate(Guid ParticipantId, string Value, string? Unit = null);
public sealed record MetricRankResult(Guid ParticipantId, int Rank, MetricValue Value);

public static class MetricFramework
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IReadOnlyList<MetricDefinition> ParseDefinitions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Metric definitions must be a JSON array.", nameof(json));

        var definitions = document.RootElement.EnumerateArray()
            .Select(ParseDefinition)
            .ToArray();
        var duplicate = definitions.GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Metric code '{duplicate.Key}' is duplicated.", nameof(json));
        return definitions;
    }

    public static string NormalizeDefinitions(string json)
    {
        var root = JsonNode.Parse(json) as JsonArray
            ?? throw new ArgumentException("Metric definitions must be a JSON array.", nameof(json));
        ParseDefinitions(json);

        foreach (var node in root.OfType<JsonObject>())
        {
            var definition = ParseDefinition(JsonSerializer.SerializeToElement(node));
            node["validation"] ??= new JsonObject();
            var validation = (JsonObject)node["validation"]!;
            validation["precision"] ??= definition.Validation.Precision;
            if (definition.Validation.Minimum is not null)
                validation["minimum"] ??= definition.Validation.Minimum.Value;
            if (definition.Validation.Maximum is not null)
                validation["maximum"] ??= definition.Validation.Maximum.Value;
            if (definition.Validation.MaximumLength is not null)
                validation["maximumLength"] ??= definition.Validation.MaximumLength.Value;
            if (definition.Validation.Pattern is not null)
                validation["pattern"] ??= definition.Validation.Pattern;
            if (definition.Validation.AllowedValues.Count > 0)
                validation["allowedValues"] ??= new JsonArray(
                    definition.Validation.AllowedValues
                        .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            if (definition.Validation.Components.Count > 0)
                validation["components"] ??= new JsonArray(
                    definition.Validation.Components
                        .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            node["sortDirection"] ??= definition.SortDirection.ToString();
            if (definition.Unit is not null) node["unit"] ??= definition.Unit;
        }

        return root.ToJsonString(JsonOptions);
    }

    public static MetricDefinition ParseDefinition(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Every metric definition must be an object.");
        var code = RequiredString(element, "id", "code");
        var name = RequiredString(element, "name");
        var rawType = RequiredString(element, "type");
        var dataType = ParseDataType(rawType);
        var validation = Property(element, "validation");
        if (validation is { ValueKind: not JsonValueKind.Object })
            throw new ArgumentException($"Metric '{code}' validation must be an object.");

        var precision = Integer(validation, "precision") ?? Integer(element, "precision")
            ?? DefaultPrecision(dataType);
        if (precision is < 0 or > 6)
            throw new ArgumentException($"Metric '{code}' precision must be between 0 and 6.");
        if (dataType == MetricDataType.Integer && precision != 0)
            throw new ArgumentException($"Integer metric '{code}' must use precision 0.");

        var minimum = Decimal(validation, "minimum", "min") ?? Decimal(element, "minimum", "min");
        var maximum = Decimal(validation, "maximum", "max") ?? Decimal(element, "maximum", "max");
        if (minimum is not null && maximum is not null && minimum > maximum)
            throw new ArgumentException($"Metric '{code}' minimum cannot exceed its maximum.");
        var maximumLength = Integer(validation, "maximumLength", "maxLength")
            ?? Integer(element, "maximumLength", "maxLength");
        if (maximumLength is <= 0)
            throw new ArgumentException($"Metric '{code}' maximum length must be positive.");
        var pattern = String(validation, "pattern") ?? String(element, "pattern");
        if (pattern is not null)
        {
            try { _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException exception)
            {
                throw new ArgumentException($"Metric '{code}' validation pattern is invalid.", exception);
            }
        }

        var allowedValues = Strings(validation, "allowedValues", "values", "options");
        if (allowedValues.Count == 0)
            allowedValues = Strings(element, "allowedValues", "values", "options");
        if (dataType == MetricDataType.Enumeration && allowedValues.Count == 0)
            throw new ArgumentException($"Enumeration metric '{code}' requires allowed values.");
        if (allowedValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allowedValues.Count)
            throw new ArgumentException($"Metric '{code}' contains duplicate allowed values.");

        var components = Strings(validation, "components");
        if (components.Count == 0) components = Strings(element, "components");
        if (dataType == MetricDataType.CompositeScore && components.Count == 0)
            throw new ArgumentException($"Composite score metric '{code}' requires components.");
        if (components.Distinct(StringComparer.OrdinalIgnoreCase).Count() != components.Count)
            throw new ArgumentException($"Metric '{code}' contains duplicate components.");

        var unit = String(element, "unit") ?? String(validation, "unit");
        if (rawType.Equals("Percentage", StringComparison.OrdinalIgnoreCase) && unit is null)
            unit = "%";
        var sortDirection = ParseSortDirection(
            String(element, "sortDirection"), String(element, "aggregation"));
        return new MetricDefinition(
            code, name, dataType, unit, sortDirection,
            new MetricValidation(minimum, maximum, precision, maximumLength, pattern,
                allowedValues, components));
    }

    public static MetricValue ValidateValue(
        MetricDefinition definition,
        string rawValue,
        string? suppliedUnit = null)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new ArgumentException($"Metric '{definition.Code}' requires a value.", nameof(rawValue));
        var unit = NormalizeUnit(definition, suppliedUnit);
        var value = rawValue.Trim();

        return definition.DataType switch
        {
            MetricDataType.Integer => Numeric(definition, value, unit, integer: true),
            MetricDataType.Decimal or MetricDataType.Distance or MetricDataType.Weight =>
                Numeric(definition, value, unit, integer: false),
            MetricDataType.Duration => Duration(definition, value, unit),
            MetricDataType.ClockTime => ClockTime(definition, value, unit),
            MetricDataType.Boolean => Boolean(definition, value, unit),
            MetricDataType.Text => Text(definition, value, unit),
            MetricDataType.Enumeration => Enumeration(definition, value, unit),
            MetricDataType.CompositeScore => Composite(definition, value, unit),
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };
    }

    public static IReadOnlyList<MetricRankResult> Rank(
        MetricDefinition definition,
        IEnumerable<MetricRankCandidate> candidates)
    {
        if (definition.SortDirection == MetricSortDirection.None)
            throw new InvalidOperationException($"Metric '{definition.Code}' does not define a ranking direction.");
        var values = candidates.Select(candidate => new
        {
            candidate.ParticipantId,
            Value = ValidateValue(definition, candidate.Value, candidate.Unit)
        }).ToArray();
        if (values.Select(item => item.ParticipantId).Distinct().Count() != values.Length)
            throw new ArgumentException("Each ranking participant must be unique.", nameof(candidates));

        var comparer = Comparer<MetricValue>.Create((left, right) => Compare(definition, left, right));
        var ordered = definition.SortDirection == MetricSortDirection.Ascending
            ? values.OrderBy(item => item.Value, comparer).ToArray()
            : values.OrderByDescending(item => item.Value, comparer).ToArray();
        var results = new List<MetricRankResult>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var rank = index == 0 || Compare(definition, ordered[index - 1].Value, ordered[index].Value) != 0
                ? index + 1
                : results[^1].Rank;
            results.Add(new MetricRankResult(ordered[index].ParticipantId, rank, ordered[index].Value));
        }
        return results;
    }

    private static MetricValue Numeric(
        MetricDefinition definition, string value, string? unit, bool integer)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            throw new ArgumentException($"Metric '{definition.Code}' requires a numeric value.");
        if (integer && decimal.Truncate(number) != number)
            throw new ArgumentException($"Metric '{definition.Code}' requires an integer value.");
        number = decimal.Round(number, definition.Validation.Precision, MidpointRounding.AwayFromZero);
        ValidateRange(definition, number);
        var serialized = Format(number, definition.Validation.Precision);
        return new MetricValue(definition.DataType, serialized, unit,
            definition.Validation.Precision, number, null, Array.Empty<decimal>());
    }

    private static MetricValue Duration(MetricDefinition definition, string value, string? unit)
    {
        decimal seconds;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out seconds))
        {
            if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration))
                throw new ArgumentException($"Metric '{definition.Code}' requires seconds or a duration.");
            seconds = (decimal)duration.TotalSeconds;
        }
        seconds = decimal.Round(seconds, definition.Validation.Precision, MidpointRounding.AwayFromZero);
        ValidateRange(definition, seconds);
        return new MetricValue(definition.DataType, Format(seconds, definition.Validation.Precision),
            unit ?? "s", definition.Validation.Precision, seconds, null, Array.Empty<decimal>());
    }

    private static MetricValue ClockTime(MetricDefinition definition, string value, string? unit)
    {
        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var clock)
            || clock < TimeSpan.Zero || clock >= TimeSpan.FromDays(1))
            throw new ArgumentException($"Metric '{definition.Code}' requires a clock time between 00:00:00 and 23:59:59.");
        var seconds = decimal.Round((decimal)clock.TotalSeconds,
            definition.Validation.Precision, MidpointRounding.AwayFromZero);
        ValidateRange(definition, seconds);
        var serialized = definition.Validation.Precision == 0
            ? clock.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : clock.ToString(@"hh\:mm\:ss\.FFFFFF", CultureInfo.InvariantCulture)
                .TrimEnd('0').TrimEnd('.');
        return new MetricValue(definition.DataType, serialized, unit,
            definition.Validation.Precision, seconds, serialized, Array.Empty<decimal>());
    }

    private static MetricValue Boolean(MetricDefinition definition, string value, string? unit)
    {
        if (!bool.TryParse(value, out var parsed))
            throw new ArgumentException($"Metric '{definition.Code}' requires true or false.");
        return new MetricValue(definition.DataType, parsed ? "true" : "false", unit, 0,
            parsed ? 1 : 0, parsed ? "true" : "false", Array.Empty<decimal>());
    }

    private static MetricValue Text(MetricDefinition definition, string value, string? unit)
    {
        if (definition.Validation.MaximumLength is not null
            && value.Length > definition.Validation.MaximumLength)
            throw new ArgumentException($"Metric '{definition.Code}' cannot exceed {definition.Validation.MaximumLength} characters.");
        if (definition.Validation.Pattern is not null
            && !Regex.IsMatch(value, definition.Validation.Pattern,
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new ArgumentException($"Metric '{definition.Code}' does not match its validation pattern.");
        return new MetricValue(definition.DataType, value, unit, 0, null, value, Array.Empty<decimal>());
    }

    private static MetricValue Enumeration(MetricDefinition definition, string value, string? unit)
    {
        var canonical = definition.Validation.AllowedValues.FirstOrDefault(item =>
            item.Equals(value, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Metric '{definition.Code}' value '{value}' is not allowed.");
        return new MetricValue(definition.DataType, canonical, unit, 0, null, canonical, Array.Empty<decimal>());
    }

    private static MetricValue Composite(MetricDefinition definition, string value, string? unit)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(value); }
        catch (JsonException exception)
        {
            throw new ArgumentException($"Metric '{definition.Code}' requires a JSON object score.", exception);
        }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException($"Metric '{definition.Code}' requires a JSON object score.");
            var parts = new List<decimal>();
            var normalized = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var component in definition.Validation.Components)
            {
                if (!TryProperty(document.RootElement, component, out var property)
                    || property.ValueKind != JsonValueKind.Number
                    || !property.TryGetDecimal(out var number))
                    throw new ArgumentException($"Metric '{definition.Code}' requires numeric component '{component}'.");
                number = decimal.Round(number, definition.Validation.Precision, MidpointRounding.AwayFromZero);
                parts.Add(number);
                normalized[component] = number;
            }
            return new MetricValue(definition.DataType,
                JsonSerializer.Serialize(normalized, JsonOptions), unit,
                definition.Validation.Precision, null, JsonSerializer.Serialize(normalized, JsonOptions), parts);
        }
    }

    private static int Compare(MetricDefinition definition, MetricValue left, MetricValue right)
    {
        if (left.NumericValue is not null && right.NumericValue is not null)
            return left.NumericValue.Value.CompareTo(right.NumericValue.Value);
        if (definition.DataType == MetricDataType.CompositeScore)
        {
            for (var index = 0; index < left.CompositeParts.Count; index++)
            {
                var comparison = left.CompositeParts[index].CompareTo(right.CompositeParts[index]);
                if (comparison != 0) return comparison;
            }
            return 0;
        }
        if (definition.DataType == MetricDataType.Enumeration)
        {
            var leftIndex = definition.Validation.AllowedValues.IndexOf(left.SerializedValue);
            var rightIndex = definition.Validation.AllowedValues.IndexOf(right.SerializedValue);
            return leftIndex.CompareTo(rightIndex);
        }
        return StringComparer.OrdinalIgnoreCase.Compare(left.SerializedValue, right.SerializedValue);
    }

    private static string? NormalizeUnit(MetricDefinition definition, string? suppliedUnit)
    {
        var normalized = string.IsNullOrWhiteSpace(suppliedUnit) ? definition.Unit : suppliedUnit.Trim();
        if (definition.Unit is not null && normalized is not null
            && !definition.Unit.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Metric '{definition.Code}' requires unit '{definition.Unit}'.");
        return normalized;
    }

    private static void ValidateRange(MetricDefinition definition, decimal value)
    {
        if (definition.Validation.Minimum is not null && value < definition.Validation.Minimum)
            throw new ArgumentException($"Metric '{definition.Code}' must be at least {definition.Validation.Minimum}.");
        if (definition.Validation.Maximum is not null && value > definition.Validation.Maximum)
            throw new ArgumentException($"Metric '{definition.Code}' must not exceed {definition.Validation.Maximum}.");
    }

    private static string Format(decimal value, int precision) =>
        value.ToString(precision == 0 ? "0" : $"0.{new string('0', precision)}", CultureInfo.InvariantCulture);

    private static MetricDataType ParseDataType(string value)
    {
        var normalized = value.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (normalized.Equals("Percentage", StringComparison.OrdinalIgnoreCase)) return MetricDataType.Decimal;
        return Enum.TryParse<MetricDataType>(normalized, true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Metric type '{value}' is not supported.");
    }

    private static MetricSortDirection ParseSortDirection(string? direction, string? aggregation)
    {
        if (!string.IsNullOrWhiteSpace(direction))
            return Enum.TryParse<MetricSortDirection>(direction, true, out var parsed)
                ? parsed
                : throw new ArgumentException($"Metric sort direction '{direction}' is not supported.");
        return aggregation?.ToLowerInvariant() switch
        {
            "min" => MetricSortDirection.Ascending,
            "max" or "sum" => MetricSortDirection.Descending,
            _ => MetricSortDirection.None
        };
    }

    private static int DefaultPrecision(MetricDataType dataType) => dataType switch
    {
        MetricDataType.Integer or MetricDataType.Boolean or MetricDataType.Text
            or MetricDataType.Enumeration => 0,
        MetricDataType.Duration or MetricDataType.Distance => 3,
        MetricDataType.ClockTime or MetricDataType.Weight or MetricDataType.Decimal
            or MetricDataType.CompositeScore => 2,
        _ => 0
    };

    private static string RequiredString(JsonElement element, params string[] names) =>
        String(element, names) is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Metric definition requires '{names[0]}'.");

    private static string? String(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (TryProperty(element.Value, name, out var property)
                && property.ValueKind == JsonValueKind.String)
                return property.GetString()?.Trim();
        return null;
    }

    private static int? Integer(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (TryProperty(element.Value, name, out var property)
                && property.TryGetInt32(out var value)) return value;
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

    private static IReadOnlyList<string> Strings(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        foreach (var name in names)
        {
            if (!TryProperty(element.Value, name, out var property)
                || property.ValueKind != JsonValueKind.Array) continue;
            return property.EnumerateArray().Select(item => item.ValueKind switch
                {
                    JsonValueKind.String => item.GetString()?.Trim(),
                    JsonValueKind.Object => String(item, "code", "id", "name"),
                    _ => null
                })
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
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

internal static class MetricFrameworkListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index].Equals(value, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }
}
