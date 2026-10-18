using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary>
/// The tenant's version as a precondition: the <c>ETag</c> is the quoted <c>version</c> (<c>xmin</c>). A change may send
/// <c>If-Match</c>, a body <c>version</c>, or both; each one sent must be the current version, otherwise
/// <c>TENANT_CONCURRENCY_CONFLICT</c>.
/// </summary>
internal static class TenantVersion
{
    public static EntityTagHeaderValue EntityTag(uint version) => new($"\"{version}\"");

    public static bool IsSatisfied(uint current, uint? bodyVersion, IfMatch ifMatch) =>
        (bodyVersion is null || bodyVersion == current) && ifMatch.Matches(EntityTag(current));

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
