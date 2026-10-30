using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Sports;

/// <summary>
/// <c>/api/admin/v1/sports</c> (<c>sport.read</c>) and <c>/api/platform/v1/sports</c> (<c>platform.sport.read</c>): the
/// same reads and DTOs in both (S05 D16).
/// </summary>
internal static class SportEndpoints
{
    public const string Tag = "Sports";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder api) => api.MapGroup("/sports").WithTags(Tag);
}
