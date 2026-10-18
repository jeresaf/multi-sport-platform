using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants;

/// <summary><c>/api/platform/v1/tenants</c>.</summary>
internal static class TenantEndpoints
{
    public const string Tag = "Tenants";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder platform) => platform.MapGroup("/tenants").WithTags(Tag);
}
