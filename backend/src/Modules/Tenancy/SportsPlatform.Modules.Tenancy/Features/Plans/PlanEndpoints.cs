using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Tenancy.Features.Plans;

/// <summary><c>/api/platform/v1/plans</c>.</summary>
internal static class PlanEndpoints
{
    public const string Tag = "Plans";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder platform) => platform.MapGroup("/plans").WithTags(Tag);
}
