using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Identity.Features.Capabilities;

/// <summary>
/// <c>/api/admin/v1/me/capabilities</c> and <c>/api/platform/v1/me/capabilities</c> (S03-T5): the actions the caller may
/// perform, for the admin app to show only those. The server still authorizes every request.
/// </summary>
internal static class CapabilitiesEndpoints
{
    public const string Tag = "Capabilities";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder family) => family.MapGroup("/me/capabilities").WithTags(Tag);
}
