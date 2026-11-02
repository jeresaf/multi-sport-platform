using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.SportsConfiguration.Features.Formats;

/// <summary>
/// <c>/api/admin/v1/formats</c> (<c>format.read</c>) and <c>/api/platform/v1/formats</c> (<c>platform.sport.read</c>):
/// the same reads and DTOs in both (S05 D14, D16).
/// </summary>
internal static class FormatEndpoints
{
    public const string Tag = "Formats";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder api) => api.MapGroup("/formats").WithTags(Tag);
}
