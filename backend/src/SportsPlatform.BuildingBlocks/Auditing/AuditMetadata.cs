using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.BuildingBlocks.Auditing;

/// <summary>
/// Serializes an audit entry's metadata with personal data masked (AR-24, ADR-0015): every property classified
/// <see cref="PersonalDataClass.Private"/> or <see cref="PersonalDataClass.Sensitive"/>, at any depth, is written as
/// <see cref="Redacted"/>, whatever its value. Redaction happens before the record leaves the request, so neither the
/// outbox payload nor the audit table ever holds the value. Unclassified members (anonymous types, dictionaries) are
/// written as they are: put anything personal in a record with classified properties.
/// </summary>
public static class AuditMetadata
{
    public const string Redacted = "[REDACTED]";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { MaskPersonalData } },
    };

    /// <summary>The metadata as redacted JSON, or null for none.</summary>
    public static string? Serialize(object? metadata) =>
        metadata is null ? null : JsonSerializer.Serialize(metadata, metadata.GetType(), _options);

    private static void MaskPersonalData(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = 0; i < typeInfo.Properties.Count; i++)
        {
            var property = typeInfo.Properties[i];
            if (property.AttributeProvider is not MemberInfo member
                || member.GetCustomAttribute<PersonalDataAttribute>() is not { IsMasked: true })
            {
                continue;
            }

            // A string property with the same name replaces the original, so non-string values are masked too.
            var masked = typeInfo.CreateJsonPropertyInfo(typeof(string), property.Name);
            masked.Get = _ => Redacted;
            masked.Order = property.Order;
            typeInfo.Properties[i] = masked;
        }
    }
}
