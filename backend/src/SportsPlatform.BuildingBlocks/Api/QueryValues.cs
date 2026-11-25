using Microsoft.AspNetCore.Builder;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>The values a query parameter accepts, for OpenAPI: <see cref="QueryValues.DocumentsQueryValues"/>.</summary>
public sealed record QueryValuesMetadata(string Name, IReadOnlyList<string> Values);

/// <summary>
/// The values a path parameter bound as text accepts, for OpenAPI: <see cref="QueryValues.DocumentsPathValues"/>.
/// </summary>
public sealed record PathValuesMetadata(string Name, IReadOnlyList<string> Values);

/// <summary>
/// A query parameter that takes a whole number, for OpenAPI: <see cref="QueryValues.DocumentsQueryInteger"/>. A null
/// <paramref name="Maximum"/> has no upper bound; a null <paramref name="Default"/> means the parameter has no default.
/// </summary>
public sealed record QueryIntegerMetadata(string Name, long Minimum, long? Maximum = null, long? Default = null);

/// <summary>A query parameter that takes a UUID, for OpenAPI: <see cref="QueryValues.DocumentsQueryUuid"/>.</summary>
public sealed record QueryUuidMetadata(string Name);

/// <summary>
/// Query parameters are bound as text, so a bad value is <c>VALIDATION_INVALID_QUERY</c> from the request's validator
/// rather than the framework's 400; OpenAPI would then show a free string. An endpoint declares
/// <c>.DocumentsQueryValues(name, values)</c> so the document shows the parameter's <c>enum</c> (S03 follow-up, S04 D14), or
/// <c>.DocumentsQueryInteger(name, minimum, maximum, @default)</c> for an integer with its bounds and the value used
/// without it (a list's <c>limit</c>: 1, 100, 25), or <c>.DocumentsQueryUuid(name)</c> for an ID (a string in the
/// <c>uuid</c> format). <c>.DocumentsPathValues(name, values)</c> does the same as the first for a path segment that
/// the handler checks itself (the public asset route's variant, S07-T4a).
/// </summary>
public static class QueryValues
{
    public static RouteHandlerBuilder DocumentsQueryValues(this RouteHandlerBuilder builder, string name, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);
        return builder.WithMetadata(new QueryValuesMetadata(name, [.. values]));
    }

    public static RouteHandlerBuilder DocumentsPathValues(this RouteHandlerBuilder builder, string name, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(values);
        return builder.WithMetadata(new PathValuesMetadata(name, [.. values]));
    }

    public static RouteHandlerBuilder DocumentsQueryInteger(
        this RouteHandlerBuilder builder, string name, long minimum, long? maximum = null, long? @default = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "The maximum is below the minimum.");
        }

        if (@default < minimum || @default > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(@default), "The default is outside the bounds.");
        }

        return builder.WithMetadata(new QueryIntegerMetadata(name, minimum, maximum, @default));
    }

    public static RouteHandlerBuilder DocumentsQueryUuid(this RouteHandlerBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return builder.WithMetadata(new QueryUuidMetadata(name));
    }
}
