using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Platform.Features.Reference;

/// <summary>
/// The public reference lists under <c>/api/public/v1/reference</c>: active entries only, ordered by code, cacheable
/// by HTTP caches for an hour (docs/architecture/api-conventions.md, "Caching public reads").
/// </summary>
internal static class ReferenceEndpoints
{
    public const string Tag = "Reference";

    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    public static RouteGroupBuilder Group(IEndpointRouteBuilder publicApi) => publicApi.MapGroup("/reference").WithTags(Tag);
}
