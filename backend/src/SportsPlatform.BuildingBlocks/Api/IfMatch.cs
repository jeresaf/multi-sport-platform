using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// A request's <c>If-Match</c> precondition (RFC 9110 §13.1.1; docs/architecture/api-conventions.md, "Concurrency"):
/// absent, <c>*</c> (any current representation) or a list of entity tags compared strongly, so a weak <c>W/</c> tag
/// never matches. The handler checks it against the resource's current tag; a mismatch is the module's
/// <c>*_CONCURRENCY_CONFLICT</c>.
/// </summary>
public sealed class IfMatch
{
    private readonly IReadOnlyList<EntityTagHeaderValue> _tags;

    private IfMatch(IReadOnlyList<EntityTagHeaderValue> tags) => _tags = tags;

    /// <summary>No <c>If-Match</c> header: every current version matches.</summary>
    public static IfMatch None { get; } = new([]);

    public bool IsPresent => _tags.Count > 0;

    /// <summary>
    /// Parses the header; false when it is present but not a valid <c>*</c> or entity-tag list (the endpoint answers
    /// <c>VALIDATION_MALFORMED_REQUEST</c>).
    /// </summary>
    public static bool TryParse(StringValues header, out IfMatch ifMatch)
    {
        ifMatch = None;
        if (StringValues.IsNullOrEmpty(header))
        {
            return true;
        }

        // RFC 9110: "*" / #entity-tag, so "*" never shares the header with tags.
        if (!EntityTagHeaderValue.TryParseStrictList(header, out var tags) || tags.Count == 0
            || (tags.Count > 1 && tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any))))
        {
            return false;
        }

        ifMatch = new IfMatch([.. tags]);
        return true;
    }

    /// <summary>Whether the precondition holds for a resource whose current tag is <paramref name="current"/>.</summary>
    public bool Matches(EntityTagHeaderValue current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return !IsPresent
            || _tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(current, useStrongComparison: true));
    }
}
