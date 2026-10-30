using Microsoft.AspNetCore.Builder;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>The values a query parameter accepts, for OpenAPI: <see cref="QueryValues.DocumentsQueryValues"/>.</summary>
public sealed record QueryValuesMetadata(string Name, IReadOnlyList<string> Values);

/// <summary>A query parameter that takes a whole number, for OpenAPI: <see cref="QueryValues.DocumentsQueryInteger"/>.</summary>
public sealed record QueryIntegerMetadata(string Name, long Minimum);

/// <summary>
/// Query parameters are bound as text, so a bad value is <c>VALIDATION_INVALID_QUERY</c> from the request's validator
/// rather than the framework's 400; OpenAPI would then show a free string. An endpoint declares
/// <c>.DocumentsQueryValues(name, values)</c> so the document shows the parameter's <c>enum</c> (S03 follow-up, S04 D14), or
/// <c>.DocumentsQueryInteger(name, minimum)</c> for an integer with its minimum.
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

    public static RouteHandlerBuilder DocumentsQueryInteger(this RouteHandlerBuilder builder, string name, long minimum)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return builder.WithMetadata(new QueryIntegerMetadata(name, minimum));
    }
}
