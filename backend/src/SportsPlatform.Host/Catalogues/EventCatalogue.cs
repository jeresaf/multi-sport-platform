using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Host.Catalogues;

/// <summary>One payload member as it is serialized: camelCase name, a JSON-ish type, and whether it may be null.</summary>
public sealed record EventPayloadField(string Name, string Type, bool Nullable);

public sealed record EventCatalogueEntry(
    IntegrationEventName Event, string Module, IReadOnlyList<EventPayloadField> Payload, IReadOnlyList<string> Consumers);

/// <summary>The integration event types one module declares (normally every <c>IIntegrationEvent</c> in its Contracts).</summary>
public sealed record EventCatalogueSource(string Module, IReadOnlyList<Type> Types);

/// <summary>
/// Every integration event: the <c>IIntegrationEvent</c> records in each module's <c>*.Contracts</c> assembly, with their
/// payload and the consumers subscribed to them. Exported by the CLI to <c>docs/generated/events.md</c> and
/// <c>contracts/events.json</c>. An event without consumers is listed with none: the outbox marks its messages
/// <c>PROCESSED</c> without delivering them, so this is where a forgotten consumer registration shows.
/// </summary>
public static class EventCatalogue
{
    // A copy of the outbox's settings with the reflection resolver they fall back to when serializing, so the catalogue
    // reads the same members without locking the shared instance.
    private static readonly JsonSerializerOptions _payloadOptions = new(MessageSerialization.Options)
    {
        TypeInfoResolver = MessageSerialization.Options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver(),
    };

    public static IReadOnlyList<EventCatalogueEntry> Build(IEnumerable<IModule> modules, IEnumerable<IntegrationEventSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var sources = modules
            .Select(module => new EventCatalogueSource(module.Name, [.. EventTypes(ContractsAssembly(module))]))
            .ToList();
        return Build(sources, subscriptions);
    }

    public static IReadOnlyList<EventCatalogueEntry> Build(IEnumerable<EventCatalogueSource> sources, IEnumerable<IntegrationEventSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(subscriptions);

        var declared = sources
            .SelectMany(source => source.Types.Select(type => (Name: IntegrationEventName.Of(type), Type: type, source.Module)))
            .ToList();

        // Consumers subscribe to classes only (IIntegrationEventConsumer<T> where T : class), so a struct event could be
        // published but never delivered.
        var structs = declared.Where(e => e.Type.IsValueType).Select(e => e.Type.FullName).ToList();
        if (structs.Count > 0)
        {
            throw new InvalidOperationException($"Integration events must be record classes: {string.Join(", ", structs)}.");
        }

        var duplicates = declared.GroupBy(e => e.Name).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Integration events declared more than once: {string.Join(", ", duplicates)}.");
        }

        var all = subscriptions.ToList();
        var twice = all.GroupBy(s => (s.Consumer, s.EventName)).Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Consumer} → {group.Key.EventName}").ToList();
        if (twice.Count > 0)
        {
            throw new InvalidOperationException($"Consumers subscribed more than once to the same event: {string.Join(", ", twice)}.");
        }

        var consumers = all.ToLookup(subscription => subscription.EventName, subscription => subscription.Consumer);
        var names = declared.Select(e => e.Name).ToHashSet();
        var undeclared = consumers.Where(group => !names.Contains(group.Key))
            .SelectMany(group => group.Select(consumer => $"{consumer} → {group.Key}"))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (undeclared.Count > 0)
        {
            throw new InvalidOperationException(
                $"Consumers subscribed to events no module's Contracts declare: {string.Join(", ", undeclared)}.");
        }

        return declared
            .Select(e => new EventCatalogueEntry(
                e.Name, e.Module, Payload(e.Name, e.Type), [.. consumers[e.Name].Order(StringComparer.Ordinal)]))
            .OrderBy(entry => entry.Event.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Event.Version)
            .ToList();
    }

    /// <summary>The module's <c>&lt;module assembly&gt;.Contracts</c> assembly, where its integration events live.</summary>
    public static Assembly ContractsAssembly(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        var name = $"{module.GetType().Assembly.GetName().Name}.Contracts";
        try
        {
            return Assembly.Load(new AssemblyName(name));
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            throw new InvalidOperationException($"Module {module.Name}'s Contracts assembly '{name}' can't be loaded.", exception);
        }
    }

    /// <summary>Concrete types (classes and structs) implementing <see cref="IIntegrationEvent"/>.</summary>
    public static IEnumerable<Type> EventTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetTypes().Where(type =>
            type is { IsInterface: false, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(type));
    }

    public static string ToMarkdown(IReadOnlyList<EventCatalogueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var markdown = new StringBuilder()
            .Append("# Integration events\n\n")
            .Append("Generated by `dotnet run --project backend/tools/SportsPlatform.Cli -- export-catalogues`; do not edit.\n")
            .Append("Declared in each module's Contracts as versioned records (`ResultApproved.V1`) and delivered through the\n")
            .Append("producer's outbox (docs/architecture/events-and-jobs.md). Payloads are JSON with these members; changes are\n")
            .Append("additive, and a breaking change is a new version. An event without consumers is not delivered anywhere.\n\n");
        if (entries.Count == 0)
        {
            return markdown.Append("No integration events are declared yet.\n").ToString();
        }

        markdown
            .Append("| Event | Version | Producer | Payload | Consumers |\n")
            .Append("|---|---|---|---|---|\n");
        foreach (var (name, module, payload, consumers) in entries)
        {
            var fields = payload.Count == 0 ? "—" : string.Join(", ", payload.Select(field => $"`{field.Name}: {field.Type}{(field.Nullable ? "?" : string.Empty)}`"));
            var subscribers = consumers.Count == 0 ? "—" : string.Join(", ", consumers.Select(consumer => $"`{consumer}`"));
            markdown.Append(CultureInfo.InvariantCulture, $"| `{name.Name}` | {name.Version} | {module} | {fields} | {subscribers} |\n");
        }

        return markdown.ToString();
    }

    public static string ToJson(IReadOnlyList<EventCatalogueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("events");
            foreach (var (name, module, payload, consumers) in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("name", name.Name);
                writer.WriteNumber("version", name.Version);
                writer.WriteString("module", module);
                writer.WriteStartArray("payload");
                foreach (var field in payload)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", field.Name);
                    writer.WriteString("type", field.Type);
                    writer.WriteBoolean("nullable", field.Nullable);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("consumers");
                foreach (var consumer in consumers)
                {
                    writer.WriteStringValue(consumer);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>
    /// The members the outbox serializes (<see cref="MessageSerialization"/>), in their JSON order. Payloads carry IDs and
    /// display fields, so only scalar members and lists of them are described; anything else fails the export until
    /// the catalogue can describe it truthfully.
    /// </summary>
    private static List<EventPayloadField> Payload(IntegrationEventName eventName, Type eventType) =>
        [.. _payloadOptions.GetTypeInfo(eventType).Properties
            .Where(property => property.Get is not null)
            .Select(property => new EventPayloadField(
                property.Name,
                TypeName(property.PropertyType) ?? throw new InvalidOperationException(
                    $"{eventName} member '{property.Name}' has a type the event catalogue can't describe ({property.PropertyType.Name}); use scalars or lists of them."),
                property.IsGetNullable))];

    /// <summary>The JSON shape of a scalar or a list of scalars; null for anything else.</summary>
    private static string? TypeName(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (ScalarName(type) is { } scalar)
        {
            return scalar;
        }

        // byte[] is written as a base64 string and dictionaries as objects: neither is a list.
        return type != typeof(byte[]) && !IsDictionary(type) && ElementType(type) is { } element && ScalarName(Nullable.GetUnderlyingType(element) ?? element) is { } item
            ? $"{item}[]"
            : null;
    }

    private static string? ScalarName(Type type) =>
        type == typeof(string) ? "string"
        // Web defaults have no string enum converter: an enum is written as its number.
        : type.IsEnum ? $"integer (enum {type.Name})"
        : Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "boolean",
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 => "integer",
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
            TypeCode.DateTime => "instant",
            _ when type == typeof(Guid) => "uuid",
            _ when type == typeof(DateTimeOffset) => "instant",
            _ when type == typeof(DateOnly) => "date",
            _ when type == typeof(TimeOnly) => "time",
            _ when type == typeof(TimeSpan) => "duration",
            _ => null,
        };

    private static bool IsDictionary(Type type) =>
        type.GetInterfaces().Append(type).Any(candidate => candidate.IsGenericType
            && (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    private static Type? ElementType(Type type) =>
        type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Append(type)
                .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
}
