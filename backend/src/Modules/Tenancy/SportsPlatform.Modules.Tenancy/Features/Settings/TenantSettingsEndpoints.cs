using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Tenancy.Features.Settings;

/// <summary><c>/api/admin/v1/tenant/settings</c>: the settings of the request's tenant (<c>X-Tenant-Id</c>).</summary>
internal static class TenantSettingsEndpoints
{
    public const string Tag = "Tenant settings";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/tenant/settings").WithTags(Tag);
}
