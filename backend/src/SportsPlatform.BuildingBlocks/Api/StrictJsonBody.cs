using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>The request an endpoint reads with <see cref="StrictJsonBody"/>; the Host documents it as the request body.</summary>
public sealed record StrictJsonBodyMetadata(Type RequestType);

/// <summary>
/// Reads a JSON object body into <c>TRequest</c> member by member, so an unknown member or a value of the wrong type is
/// a field error (<c>errors: [{field, code}]</c>) rather than an unreadable request. The endpoint takes the body as a
/// <see cref="JsonElement"/> (<c>[FromBody]</c>) and declares <c>.AcceptsStrictJson&lt;TRequest&gt;()</c>, so OpenAPI
/// shows <c>TRequest</c> rather than any JSON.
/// A body that is not a JSON object is <c>VALIDATION_MALFORMED_REQUEST</c>, like one that is not JSON at all. Every
/// member of <c>TRequest</c> must be optional (a missing one is null); a <c>required</c> member is a bug (it throws).
/// </summary>
public static class StrictJsonBody
{
    /// <summary>Documents <typeparamref name="TRequest"/> as the body of an endpoint that reads it with <see cref="TryRead{TRequest}"/>.</summary>
    public static RouteHandlerBuilder AcceptsStrictJson<TRequest>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new StrictJsonBodyMetadata(typeof(TRequest)));
    }

    /// <summary>
    /// Deserializes <paramref name="body"/>; on failure <paramref name="problem"/> is <paramref name="error"/> with one
    /// <c>errors</c> entry per unknown, wrongly typed or repeated member, named as the request names it (camelCase).
    /// </summary>
    public static bool TryRead<TRequest>(
        JsonElement body,
        JsonSerializerOptions options,
        Error error,
        [NotNullWhen(true)] out TRequest? request,
        [NotNullWhen(false)] out ProblemHttpResult? problem)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(error);
        request = default;

        if (body.ValueKind != JsonValueKind.Object)
        {
            problem = CommonErrors.MalformedRequest.ToProblem("The request body must be a JSON object.");
            return false;
        }

        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var typeInfo = options.GetTypeInfo(typeof(TRequest));
        if (typeInfo.Properties.FirstOrDefault(p => p.IsRequired) is { } required)
        {
            // Every member is optional here (a missing one is null); a required one would fail unnamed below.
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name}.{required.Name} is required; StrictJsonBody reads requests whose members are all optional.");
        }

        var members = typeInfo.Properties.ToDictionary(p => p.Name, comparer);
        var failures = new List<(FieldError Error, string Message)>();
        var seen = new HashSet<string>(comparer);
        foreach (var property in body.EnumerateObject())
        {
            if (!members.TryGetValue(property.Name, out var member))
            {
                failures.Add((new FieldError(property.Name, error.Code), $"'{property.Name}' is not a field of this request."));
            }
            else if (!seen.Add(member.Name))
            {
                // The serializer would keep the last value without a word.
                failures.Add((new FieldError(member.Name, error.Code), $"'{member.Name}' is sent more than once."));
            }
            else if (!HasType<TRequest>(property, options))
            {
                failures.Add((new FieldError(member.Name, error.Code), $"'{member.Name}' must be {Describe(member.PropertyType)}."));
            }
        }

        if (failures.Count > 0)
        {
            problem = error.ToProblem(string.Join(" ", failures.Select(f => f.Message)));
            problem.ProblemDetails.Extensions[ProblemFields.Errors] = failures.Select(f => f.Error).ToList();
            return false;
        }

        try
        {
            request = body.Deserialize<TRequest>(options);
        }
        catch (JsonException)
        {
            // Unreachable while every member reads on its own and none is required; answer as unreadable.
        }

        problem = request is null ? CommonErrors.MalformedRequest.ToProblem() : null;
        return request is not null;
    }

    // The member alone, read as the request: the property's own converter or number handling applies.
    private static bool HasType<TRequest>(JsonProperty property, JsonSerializerOptions options)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            property.WriteTo(writer);
            writer.WriteEndObject();
        }

        try
        {
            _ = JsonSerializer.Deserialize<TRequest>(buffer.WrittenSpan, options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // What the member accepts, without the value sent (docs/architecture/api-conventions.md).
    private static string Describe(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(bool) ? "true or false"
            : underlying == typeof(string) ? "a string"
            : underlying.IsEnum ? "one of the values the API description lists"
            : underlying.IsPrimitive || underlying == typeof(decimal) ? "a JSON number in range"
            : "a value of the type the API description shows";
    }
}
