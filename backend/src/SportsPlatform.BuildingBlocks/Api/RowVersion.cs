using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// A mutable admin resource's version as a precondition (api-conventions.md): the <c>ETag</c> is the quoted
/// <c>version</c> (<c>xmin</c>). A change may send <c>If-Match</c>, a body <c>version</c>, or both; each one sent must be
/// the current version, otherwise the module's <c>*_CONCURRENCY_CONFLICT</c>.
/// </summary>
public static class RowVersion
{
    public static EntityTagHeaderValue EntityTag(uint version) => new($"\"{version}\"");

    public static bool IsSatisfied(uint current, uint? bodyVersion, IfMatch ifMatch)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        return (bodyVersion is null || bodyVersion == current) && ifMatch.Matches(EntityTag(current));
    }

    /// <summary>Reads <c>If-Match</c>; <paramref name="problem"/> is set when the header doesn't parse.</summary>
    public static bool TryReadIfMatch(HttpRequest request, out IfMatch ifMatch, [NotNullWhen(false)] out ProblemHttpResult? problem)
    {
        ArgumentNullException.ThrowIfNull(request);
        problem = IfMatch.TryParse(request.Headers.IfMatch, out ifMatch)
            ? null
            : CommonErrors.MalformedRequest.ToProblem("'If-Match' must be * or a list of quoted entity tags, such as \"1234\".");
        return problem is null;
    }
}

/// <summary>The endpoint reads <c>If-Match</c> (<see cref="RowVersion.TryReadIfMatch"/>); the Host documents the header.</summary>
public sealed record AcceptsIfMatchMetadata;

/// <summary>The endpoint's success response carries the resource's <c>ETag</c>; the Host documents the header.</summary>
public sealed record ReturnsETagMetadata;

/// <summary>Declares an endpoint's use of the row version, so OpenAPI shows the headers (api-conventions.md, "Concurrency").</summary>
public static class RowVersionEndpointExtensions
{
    /// <summary>The endpoint takes an optional <c>If-Match</c> with the <c>ETag</c> of the last read.</summary>
    public static TBuilder AcceptsIfMatch<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new AcceptsIfMatchMetadata());
    }

    /// <summary>The endpoint's success response sets <c>ETag</c> to the quoted <c>version</c>.</summary>
    public static TBuilder ReturnsETag<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new ReturnsETagMetadata());
    }
}
