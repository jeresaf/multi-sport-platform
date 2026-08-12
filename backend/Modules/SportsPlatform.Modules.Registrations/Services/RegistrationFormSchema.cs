using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Modules.Registrations.Services;

public static class RegistrationFormSchema
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Text", "Number", "Date", "Select", "Checkbox", "File", "Consent"
    };

    public static IReadOnlyList<RegistrationFormField> Normalize(
        IEnumerable<RegistrationFormField>? fields)
    {
        var normalized = (fields ?? Array.Empty<RegistrationFormField>())
            .OrderBy(field => field.Order).ToArray();
        if (normalized.Length == 0)
            throw new ArgumentException("A registration form requires at least one field.", nameof(fields));
        if (normalized.Length > 100)
            throw new ArgumentException("A registration form cannot exceed 100 fields.", nameof(fields));

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in normalized)
        {
            field.Id = Required(field.Id, "Field ID", 100);
            if (!Regex.IsMatch(field.Id, "^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant))
                throw new ArgumentException($"Field ID '{field.Id}' is invalid.", nameof(fields));
            if (!ids.Add(field.Id))
                throw new ArgumentException($"Field ID '{field.Id}' is duplicated.", nameof(fields));
            field.Label = Required(field.Label, "Field label", 200);
            field.FieldType = SupportedTypes.FirstOrDefault(type =>
                type.Equals(field.FieldType?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Field type '{field.FieldType}' is not supported.", nameof(fields));
            field.Placeholder = Optional(field.Placeholder, 300);
            field.HelpText = Optional(field.HelpText, 1000);
            field.ValidationMessage = Optional(field.ValidationMessage, 500);
            field.ConsentText = Optional(field.ConsentText, 2000);
            if (field.MinLength is < 0 || field.MaxLength is < 0
                || field.MinLength > field.MaxLength)
                throw new ArgumentException($"Field '{field.Label}' has invalid length limits.", nameof(fields));
            if (field.MinValue > field.MaxValue)
                throw new ArgumentException($"Field '{field.Label}' has invalid numeric limits.", nameof(fields));
            if (!string.IsNullOrWhiteSpace(field.ValidationRegex))
            {
                try { _ = new Regex(field.ValidationRegex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)); }
                catch (ArgumentException) { throw new ArgumentException($"Field '{field.Label}' has an invalid validation expression.", nameof(fields)); }
            }
            if (field.FieldType is "Select" or "Checkbox")
            {
                if (field.Options is null || field.Options.Count == 0)
                    throw new ArgumentException($"Field '{field.Label}' requires options.", nameof(fields));
                var optionValues = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in field.Options)
                {
                    option.Value = Required(option.Value, "Option value", 200);
                    option.Label = Required(option.Label, "Option label", 200);
                    if (!optionValues.Add(option.Value))
                        throw new ArgumentException($"Field '{field.Label}' has duplicate option values.", nameof(fields));
                }
            }
            else field.Options = null;
            if (field.FieldType == "Consent" && string.IsNullOrWhiteSpace(field.ConsentText))
                throw new ArgumentException($"Consent field '{field.Label}' requires consent text.", nameof(fields));
            if (field.FieldType == "File")
            {
                field.MaxFileSizeBytes ??= 5 * 1024 * 1024;
                if (field.MaxFileSizeBytes is <= 0 or > 20 * 1024 * 1024)
                    throw new ArgumentException($"File field '{field.Label}' has an invalid size limit.", nameof(fields));
                field.AllowedContentTypes = (field.AllowedContentTypes ??
                    ["application/pdf", "image/jpeg", "image/png"])
                    .Select(value => Required(value, "Content type", 100))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            else
            {
                field.AllowedContentTypes = null;
                field.MaxFileSizeBytes = null;
            }
        }

        foreach (var field in normalized.Where(field => !string.IsNullOrWhiteSpace(field.ConditionalFieldId)))
        {
            field.ConditionalFieldId = field.ConditionalFieldId!.Trim();
            if (field.ConditionalFieldId.Equals(field.Id, StringComparison.OrdinalIgnoreCase)
                || !ids.Contains(field.ConditionalFieldId))
                throw new ArgumentException($"Field '{field.Label}' has an invalid conditional field.", nameof(fields));
            if (field.ConditionalValue is null)
                throw new ArgumentException($"Field '{field.Label}' requires a conditional value.", nameof(fields));
        }
        return normalized;
    }

    public static (bool IsValid, List<string> Errors) Validate(
        string schemaJson,
        IReadOnlyDictionary<string, object> formData)
    {
        var fields = JsonSerializer.Deserialize<List<RegistrationFormField>>(schemaJson) ?? [];
        var errors = new List<string>();
        foreach (var field in fields.OrderBy(field => field.Order))
        {
            if (!IsVisible(field, formData)) continue;
            var present = formData.TryGetValue(field.Id, out var value) && !IsEmpty(value);
            if (field.IsRequired && !present)
            {
                errors.Add($"Field '{field.Label}' is required.");
                continue;
            }
            if (!present) continue;
            ValidateValue(field, value!, errors);
        }
        return (errors.Count == 0, errors);
    }

    private static bool IsVisible(
        RegistrationFormField field,
        IReadOnlyDictionary<string, object> data)
    {
        if (string.IsNullOrWhiteSpace(field.ConditionalFieldId)) return true;
        if (!data.TryGetValue(field.ConditionalFieldId, out var value)) return false;
        return Scalar(value).Equals(field.ConditionalValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateValue(
        RegistrationFormField field,
        object value,
        ICollection<string> errors)
    {
        var element = AsElement(value);
        switch (field.FieldType)
        {
            case "Text":
                if (!TryString(element, out var text)) { InvalidType(field, errors); return; }
                if (field.MinLength.HasValue && text.Length < field.MinLength) errors.Add($"Field '{field.Label}' must be at least {field.MinLength} characters.");
                if (field.MaxLength.HasValue && text.Length > field.MaxLength) errors.Add($"Field '{field.Label}' must be at most {field.MaxLength} characters.");
                if (!string.IsNullOrWhiteSpace(field.ValidationRegex)
                    && !Regex.IsMatch(text, field.ValidationRegex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                    errors.Add(field.ValidationMessage ?? $"Field '{field.Label}' is invalid.");
                break;
            case "Number":
                if (!TryDecimal(element, out var number)) { InvalidType(field, errors); return; }
                if (field.MinValue.HasValue && number < field.MinValue) errors.Add($"Field '{field.Label}' must be at least {field.MinValue}.");
                if (field.MaxValue.HasValue && number > field.MaxValue) errors.Add($"Field '{field.Label}' must be at most {field.MaxValue}.");
                break;
            case "Date":
                if (!TryString(element, out var dateText) || !DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)) InvalidType(field, errors);
                break;
            case "Select":
                if (!TryString(element, out var selected)
                    || field.Options?.All(option => option.Value != selected) != false)
                    errors.Add($"Field '{field.Label}' has an invalid selection.");
                break;
            case "Checkbox":
                if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) break;
                if (element.ValueKind != JsonValueKind.Array) { InvalidType(field, errors); return; }
                foreach (var item in element.EnumerateArray())
                    if (field.Options?.All(option => option.Value != Scalar(item)) != false)
                        errors.Add($"Field '{field.Label}' contains an invalid selection.");
                break;
            case "Consent":
                if (element.ValueKind != JsonValueKind.True)
                    errors.Add($"Field '{field.Label}' must be accepted.");
                break;
            case "File":
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("url", out var url)
                    || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri)
                    || uri.Scheme is not ("http" or "https"))
                { InvalidType(field, errors); return; }
                if (element.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes)
                    && field.MaxFileSizeBytes.HasValue && bytes > field.MaxFileSizeBytes)
                    errors.Add($"Field '{field.Label}' exceeds the file size limit.");
                if (element.TryGetProperty("contentType", out var contentType)
                    && field.AllowedContentTypes?.Contains(contentType.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase) == false)
                    errors.Add($"Field '{field.Label}' has an unsupported file type.");
                break;
        }
    }

    private static void InvalidType(RegistrationFormField field, ICollection<string> errors) =>
        errors.Add($"Field '{field.Label}' has an invalid {field.FieldType.ToLowerInvariant()} value.");
    private static JsonElement AsElement(object value) => value is JsonElement element
        ? element : JsonSerializer.SerializeToElement(value);
    private static bool TryString(JsonElement value, out string result)
    { result = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty; return value.ValueKind == JsonValueKind.String; }
    private static bool TryDecimal(JsonElement value, out decimal result)
    {
        result = default;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out result);
    }
    private static bool IsEmpty(object? value)
    {
        if (value is null) return true;
        var element = AsElement(value);
        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString()))
            || (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0);
    }
    private static string Scalar(object value) => Scalar(AsElement(value));
    private static string Scalar(JsonElement value) => value.ValueKind switch
    { JsonValueKind.String => value.GetString() ?? string.Empty, JsonValueKind.True => "true", JsonValueKind.False => "false", _ => value.ToString() };
    private static string Required(string? value, string name, int maximum)
    { var normalized = value?.Trim() ?? string.Empty; if (normalized.Length == 0) throw new ArgumentException($"{name} is required."); if (normalized.Length > maximum) throw new ArgumentException($"{name} cannot exceed {maximum} characters."); return normalized; }
    private static string? Optional(string? value, int maximum)
    { if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Trim(); if (normalized.Length > maximum) throw new ArgumentException($"Value cannot exceed {maximum} characters."); return normalized; }
}
